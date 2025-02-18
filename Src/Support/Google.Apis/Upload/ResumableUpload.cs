﻿/*
Copyright 2012 Google Inc

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

using Google.Apis.Http;
using Google.Apis.Logging;
using Google.Apis.Media;
using Google.Apis.Requests;
using Google.Apis.Services;
using Google.Apis.Testing;
using Google.Apis.Util;
using System.Collections.Generic;

namespace Google.Apis.Upload
{
    /// <summary>
    /// Media upload which uses Google's resumable media upload protocol to upload data.
    /// </summary>
    /// <remarks>
    /// See: https://developers.google.com/drive/manage-uploads#resumable for more information on the protocol.
    /// </remarks>
    public abstract class ResumableUpload
    {
        #region Constants

        /// <summary>The class logger.</summary>
        private static readonly ILogger Logger = ApplicationContext.Logger.ForType<ResumableUpload>();

        private const int KB = 0x400;
        private const int MB = 0x100000;

        /// <summary>Minimum chunk size (except the last one). Default value is 256*KB.</summary>
        public const int MinimumChunkSize = 256 * KB;

        /// <summary>Default chunk size. Default value is 10*MB.</summary>
        public const int DefaultChunkSize = 10 * MB;

        /// <summary>
        /// Defines how many bytes are read from the input stream in each stream read action. 
        /// The read will continue until we read <see cref="MinimumChunkSize"/> or we reached the end of the stream.
        /// </summary>
        internal int BufferSize = 4 * KB;

        /// <summary>Indicates the stream's size is unknown.</summary>
        private const int UnknownSize = -1;
        /// <summary>Content-Range header value for the body upload of zero length files.</summary>
        private const string ZeroByteContentRangeHeader = "bytes */0";

        /// <summary>
        /// The x-goog-api-client header value used for resumable uploads initiated without any options or an HttpClient.
        /// </summary>
        private static readonly string DefaultGoogleApiClientHeader = new VersionHeaderBuilder().AppendDotNetEnvironment().AppendAssemblyVersion("gdcl", typeof(ResumableUpload)).ToString();
        #endregion // Constants

        #region Construction

        /// <summary>
        /// Creates a <see cref="ResumableUpload"/> instance.
        /// </summary>
        /// <param name="contentStream">The data to be uploaded. Must not be null.</param>
        /// <param name="options">The options for the upload operation. May be null.</param>
        protected ResumableUpload(Stream contentStream, ResumableUploadOptions options)
        {
            contentStream.ThrowIfNull(nameof(contentStream));
            ContentStream = contentStream;
            // Check if the stream length is known.
            StreamLength = ContentStream.CanSeek ? ContentStream.Length : UnknownSize;
            HttpClient = options?.ConfigurableHttpClient ?? new HttpClientFactory().CreateHttpClient(new CreateHttpClientArgs { ApplicationName = "ResumableUpload", GZipEnabled = true, GoogleApiClientHeader = DefaultGoogleApiClientHeader });
            Options = options;
        }

        /// <summary>
        /// Creates a <see cref="ResumableUpload"/> instance for a resumable upload session which has already been initiated.
        /// </summary>
        /// <remarks>
        /// See https://cloud.google.com/storage/docs/json_api/v1/how-tos/resumable-upload#start-resumable for more information about initiating
        /// resumable upload sessions and saving the session URI, or upload URI.
        /// </remarks>
        /// <param name="uploadUri">The session URI of the resumable upload session. Must not be null.</param>
        /// <param name="contentStream">The data to be uploaded. Must not be null.</param>
        /// <param name="options">The options for the upload operation. May be null.</param>
        /// <returns>The instance which can be used to upload the specified content.</returns>
        public static ResumableUpload CreateFromUploadUri(
            Uri uploadUri,
            Stream contentStream,
            ResumableUploadOptions options = null)
        {
            uploadUri.ThrowIfNull(nameof(uploadUri));
            return new InitiatedResumableUpload(uploadUri, contentStream, options);
        }

        private sealed class InitiatedResumableUpload : ResumableUpload
        {
            private Uri _initiatedUploadUri;

            public InitiatedResumableUpload(Uri uploadUri, Stream contentStream, ResumableUploadOptions options)
                : base(contentStream, options)
            {
                _initiatedUploadUri = uploadUri;
            }

            public override Task<Uri> InitiateSessionAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                return Task.FromResult(_initiatedUploadUri);
            }
        }

        #endregion // Construction

        #region Properties


        /// <summary>
        /// Gets the options used to control the resumable upload.
        /// </summary>
        protected ResumableUploadOptions Options { get; }

        /// <summary>
        /// Gets the HTTP client to use to make requests.
        /// </summary>
        internal ConfigurableHttpClient HttpClient { get; }

        /// <summary>Gets or sets the stream to upload.</summary>
        public Stream ContentStream { get; }

        /// <summary>
        /// Gets or sets the length of the steam. Will be <see cref="UnknownSize" /> if the media content length is 
        /// unknown. 
        /// </summary>
        internal long StreamLength { get; set; }

        /// <summary>
        /// Gets or sets the content of the last buffer request to the server or <c>null</c>. It is used when the media
        /// content length is unknown, for resending it in case of server error.
        /// Only used with a non-seekable stream.
        /// </summary>
        private byte[] LastMediaRequest { get; set; }

        /// <summary>
        /// Gets or sets the last request length.
        /// Only used with a non-seekable stream.
        /// </summary>
        private int LastMediaLength { get; set; }

        /// <summary>
        /// Gets or sets the resumable session URI. 
        /// See https://developers.google.com/drive/manage-uploads#save-session-uri" for more details.
        /// </summary>
        private Uri UploadUri { get; set; }

        /// <summary>Gets or sets the amount of bytes the server had received so far.</summary>
        private long BytesServerReceived { get; set; }

        /// <summary>Gets or sets the amount of bytes the client had sent so far.</summary>
        private long BytesClientSent { get; set; }

        /// <summary>Change this value ONLY for testing purposes!</summary>
        [VisibleForTestOnly]
        protected int chunkSize = DefaultChunkSize;

        /// <summary>
        /// Gets or sets the size of each chunk sent to the server.
        /// Chunks (except the last chunk) must be a multiple of <see cref="MinimumChunkSize"/> to be compatible with 
        /// Google upload servers.
        /// </summary>
        public int ChunkSize
        {
            get { return chunkSize; }
            set
            {
                if (value < MinimumChunkSize)
                {
                    throw new ArgumentOutOfRangeException("ChunkSize");
                }
                chunkSize = value;
            }
        }

        #endregion // Properties

        #region Events

        /// <summary>Event called whenever the progress of the upload changes.</summary>
        public event Action<IUploadProgress> ProgressChanged;

        /// <summary>
        /// Interceptor used to propagate data successfully uploaded on each chunk.
        /// </summary>
        public StreamInterceptor UploadStreamInterceptor { get; set; }

        #endregion //Events

        #region Error handling (Exception and 5xx)

        /// <summary>
        /// Callback class that is invoked on abnormal response or an exception.
        /// This class changes the request to query the current status of the upload in order to find how many bytes  
        /// were successfully uploaded before the error occurred.
        /// See https://developers.google.com/drive/manage-uploads#resume-upload for more details.
        /// </summary>
        class ServerErrorCallback : IHttpUnsuccessfulResponseHandler, IHttpExceptionHandler
        {
            private ResumableUpload Owner { get; set; }

            /// <summary>
            /// Constructs a new callback and register it as unsuccessful response handler and exception handler on the 
            /// configurable message handler.
            /// </summary>
            public ServerErrorCallback(ResumableUpload resumable)
            {
                this.Owner = resumable;
            }

            public void AddToRequest(HttpRequestMessage request)
            {
                // This assumes the property hasn't been set elsewhere - but that's reasonable as we're creating the messages ourselves in this class.
                request.Properties[ConfigurableMessageHandler.ExceptionHandlerKey] = new List<IHttpExceptionHandler> { this };
                request.Properties[ConfigurableMessageHandler.UnsuccessfulResponseHandlerKey] = new List<IHttpUnsuccessfulResponseHandler> { this };
            }

            public Task<bool> HandleResponseAsync(HandleUnsuccessfulResponseArgs args)
            {
                var result = false;
                var statusCode = (int)args.Response.StatusCode;
                // Handle the error if and only if all the following conditions occur:
                // - there is going to be an actual retry
                // - the message request is for media upload with the current Uri (remember that the message handler
                //   can be invoked from other threads \ messages, so we should call server error callback only if the
                //   request is in the current context).
                // - we got a 5xx server error.
                if (args.SupportsRetry && args.Request.RequestUri.Equals(Owner.UploadUri) && statusCode / 100 == 5)
                {
                    result = OnServerError(args.Request);
                }

                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                tcs.SetResult(result);
                return tcs.Task;
            }

            public Task<bool> HandleExceptionAsync(HandleExceptionArgs args)
            {
                var result = args.SupportsRetry && !args.CancellationToken.IsCancellationRequested &&
                    args.Request.RequestUri.Equals(Owner.UploadUri) ? OnServerError(args.Request) : false;

                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                tcs.SetResult(result);
                return tcs.Task;
            }

            /// <summary>Changes the request in order to resume the interrupted upload.</summary>
            private bool OnServerError(HttpRequestMessage request)
            {
                // Clear all headers and set Content-Range and Content-Length headers.
                var range = String.Format("bytes */{0}", Owner.StreamLength < 0 ? "*" : Owner.StreamLength.ToString());
                request.Headers.Clear();
                request.Method = System.Net.Http.HttpMethod.Put;
                request.SetEmptyContent().Headers.Add("Content-Range", range);
                return true;
            }
        }

        #endregion

        #region Progress Monitoring

        /// <summary>Class that communicates the progress of resumable uploads to a container.</summary>
        private class ResumableUploadProgress : IUploadProgress
        {
            /// <summary>
            /// Create a ResumableUploadProgress instance.
            /// </summary>
            /// <param name="status">The status of the upload.</param>
            /// <param name="bytesSent">The number of bytes sent so far.</param>
            public ResumableUploadProgress(UploadStatus status, long bytesSent)
            {
                Status = status;
                BytesSent = bytesSent;
            }

            /// <summary>
            /// Create a ResumableUploadProgress instance.
            /// </summary>
            /// <param name="exception">An exception that occurred during the upload.</param>
            /// <param name="bytesSent">The number of bytes sent before this exception occurred.</param>
            public ResumableUploadProgress(Exception exception, long bytesSent)
            {
                Status = UploadStatus.Failed;
                BytesSent = bytesSent;
                Exception = exception;
            }

            public UploadStatus Status { get; private set; }
            public long BytesSent { get; private set; }
            public Exception Exception { get; private set; }
        }

        /// <summary>
        /// Current state of progress of the upload.
        /// </summary>
        /// <seealso cref="ProgressChanged"/>
        private ResumableUploadProgress Progress { get; set; }

        /// <summary>
        /// Updates the current progress and call the <see cref="ProgressChanged"/> event to notify listeners.
        /// </summary>
        private void UpdateProgress(ResumableUploadProgress progress)
        {
            Progress = progress;
            ProgressChanged?.Invoke(progress);
        }

        /// <summary>
        /// Get the current progress state.
        /// </summary>
        /// <returns>An IUploadProgress describing the current progress of the upload.</returns>
        /// <seealso cref="ProgressChanged"/>
        public IUploadProgress GetProgress()
        {
            return Progress;
        }

        #endregion

        #region UploadSessionData
        /// <summary>
        /// Event called when an UploadUri is created. 
        /// Not needed if the application program will not support resuming after a program restart.
        /// </summary>
        /// <remarks>
        /// Within the event, persist the UploadUri to storage.
        /// It is strongly recommended that the full path filename (or other media identifier) is also stored so that it can be compared to the current open filename (media) upon restart.
        /// </remarks>
        public event Action<IUploadSessionData> UploadSessionData;
        /// <summary>
        /// Data to be passed to the application program to allow resuming an upload after a program restart.
        /// </summary>
        private class ResumeableUploadSessionData : IUploadSessionData
        {
            /// <summary>
            /// Create a ResumeableUploadSessionData instance to pass the UploadUri to the client.
            /// </summary>
            /// <param name="uploadUri">The resumable session URI.</param>
            public ResumeableUploadSessionData(Uri uploadUri)
            {
                UploadUri = uploadUri;
            }
            public Uri UploadUri { get; private set; }
        }
        /// <summary>
        /// Send data (UploadUri) to application so it can store it to persistent storage.
        /// </summary>
        private void SendUploadSessionData(ResumeableUploadSessionData sessionData)
        {
            UploadSessionData?.Invoke(sessionData);
        }
        #endregion

        #region Upload Implementation

        /// <summary>
        /// Uploads the content to the server. This method is synchronous and will block until the upload is completed.
        /// </summary>
        /// <remarks>
        /// In case the upload fails the <see cref="IUploadProgress.Exception"/> will contain the exception that
        /// cause the failure.
        /// </remarks>
        public IUploadProgress Upload()
        {
            return UploadAsync(CancellationToken.None).Result;
        }

        /// <summary>Uploads the content asynchronously to the server.</summary>
        public Task<IUploadProgress> UploadAsync()
        {
            return UploadAsync(CancellationToken.None);
        }

        /// <summary>Uploads the content to the server using the given cancellation token.</summary>
        /// <remarks>
        /// In case the upload fails <see cref="IUploadProgress.Exception"/> will contain the exception that
        /// cause the failure. The only exception which will be thrown is 
        /// <see cref="System.Threading.Tasks.TaskCanceledException"/> which indicates that the task was canceled.
        /// </remarks>
        /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
        public async Task<IUploadProgress> UploadAsync(CancellationToken cancellationToken)
        {
            BytesServerReceived = 0;
            UpdateProgress(new ResumableUploadProgress(UploadStatus.Starting, 0));

            try
            {
                UploadUri = await InitiateSessionAsync(cancellationToken).ConfigureAwait(false);
                if (ContentStream.CanSeek)
                {
                    SendUploadSessionData(new ResumeableUploadSessionData(UploadUri));
                }
                Logger.Debug("MediaUpload[{0}] - Start uploading...", UploadUri);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "MediaUpload - Exception occurred while initializing the upload");
                UpdateProgress(new ResumableUploadProgress(ex, BytesServerReceived));
                return Progress;
            }

            return await UploadCoreAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resumes the upload from the last point it was interrupted. 
        /// Use when resuming and the program was not restarted.
        /// </summary>
        public IUploadProgress Resume()
        {
            return ResumeAsync(null, CancellationToken.None).Result;
        }
        /// <summary>
        /// Resumes the upload from the last point it was interrupted. 
        /// Use when the program was restarted and you wish to resume the upload that was in progress when the program was halted. 
        /// Implemented only for ContentStreams where .CanSeek is True.
        /// </summary>
        /// <remarks>
        /// In your application's UploadSessionData Event Handler, store UploadUri.AbsoluteUri property value (resumable session URI string value)
        /// to persistent storage for use with Resume() or ResumeAsync() upon a program restart.
        /// It is strongly recommended that the FullPathFilename of the media file that is being uploaded is saved also so that a subsequent execution of the
        /// program can compare the saved FullPathFilename value to the FullPathFilename of the media file that it has opened for uploading.
        /// You do not need to seek to restart point in the ContentStream file.
        /// </remarks>
        /// <param name="uploadUri">VideosResource.InsertMediaUpload UploadUri property value that was saved to persistent storage during a prior execution.</param>
        public IUploadProgress Resume(Uri uploadUri)
        {
            return ResumeAsync(uploadUri, CancellationToken.None).Result;
        }
        /// <summary>
        /// Asynchronously resumes the upload from the last point it was interrupted.
        /// </summary>
        /// <remarks>
        /// You do not need to seek to restart point in the ContentStream file.
        /// </remarks>
        public Task<IUploadProgress> ResumeAsync()
        {
            return ResumeAsync(null, CancellationToken.None);
        }
        /// <summary>
        /// Asynchronously resumes the upload from the last point it was interrupted. 
        /// Use when resuming and the program was not restarted.
        /// </summary>
        /// <remarks>
        /// You do not need to seek to restart point in the ContentStream file.
        /// </remarks>
        /// <param name="cancellationToken">A cancellation token to cancel the asynchronous operation.</param>
        public Task<IUploadProgress> ResumeAsync(CancellationToken cancellationToken)
        {
            return ResumeAsync(null, cancellationToken);
        }
        /// <summary>
        /// Asynchronously resumes the upload from the last point it was interrupted. 
        /// Use when resuming and the program was restarted.
        /// Implemented only for ContentStreams where .CanSeek is True.
        /// </summary>
        /// <remarks>
        /// In your application's UploadSessionData Event Handler, store UploadUri.AbsoluteUri property value (resumable session URI string value)
        /// to persistent storage for use with Resume() or ResumeAsync() upon a program restart.
        /// It is strongly recommended that the FullPathFilename of the media file that is being uploaded is saved also so that a subsequent execution of the
        /// program can compare the saved FullPathFilename value to the FullPathFilename of the media file that it has opened for uploading.
        /// You do not need to seek to restart point in the ContentStream file.
        /// </remarks>
        /// <param name="uploadUri">VideosResource.InsertMediaUpload UploadUri property value that was saved to persistent storage during a prior execution.</param>
        public Task<IUploadProgress> ResumeAsync(Uri uploadUri)
        {
            return ResumeAsync(uploadUri, CancellationToken.None);
        }
        /// <summary>
        /// Asynchronously resumes the upload from the last point it was interrupted. 
        /// Use when the program was restarted and you wish to resume the upload that was in progress when the program was halted.
        /// Implemented only for ContentStreams where .CanSeek is True.
        /// </summary>
        /// <remarks>
        /// In your application's UploadSessionData Event Handler, store UploadUri.AbsoluteUri property value (resumable session URI string value)
        /// to persistent storage for use with Resume() or ResumeAsync() upon a program restart.
        /// It is strongly recommended that the FullPathFilename of the media file that is being uploaded is saved also so that a subsequent execution of the
        /// program can compare the saved FullPathFilename value to the FullPathFilename of the media file that it has opened for uploading.
        /// You do not need to seek to restart point in the ContentStream file.
        /// </remarks>
        /// <param name="uploadUri">VideosResource.InsertMediaUpload UploadUri property value that was saved to persistent storage during a prior execution.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the asynchronous operation.</param>
        public async Task<IUploadProgress> ResumeAsync(Uri uploadUri, CancellationToken cancellationToken)
        {
            // When called with uploadUri parameter of non-null value, the UploadUri is being
            // provided upon a program restart to resume a previously interrupted upload.
            if (uploadUri != null)
            {
                if (ContentStream.CanSeek)
                {
                    Logger.Info("Resuming after program restart: UploadUri={0}", uploadUri);
                    UploadUri = uploadUri;
                }
                else
                {
                    throw new NotImplementedException("Resume after program restart not allowed when ContentStream.CanSeek is false");
                }
            }
            if (UploadUri == null)
            {
                Logger.Info("There isn't any upload in progress, so starting to upload again");
                return await UploadAsync(cancellationToken).ConfigureAwait(false);
            }
            // The first "resuming" request is to query the server in which point the upload was interrupted.
            var range = String.Format("bytes */{0}", StreamLength < 0 ? "*" : StreamLength.ToString());
            HttpRequestMessage request = new RequestBuilder()
            {
                BaseUri = UploadUri,
                Method = HttpConsts.Put
            }.CreateRequest();
            request.SetEmptyContent().Headers.Add("Content-Range", range);

            try
            {
                HttpResponseMessage response;
                new ServerErrorCallback(this).AddToRequest(request);
                response = await HttpClient.SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                if (await HandleResponse(response).ConfigureAwait(false))
                {
                    // All the media was successfully upload.
                    UpdateProgress(new ResumableUploadProgress(UploadStatus.Completed, BytesServerReceived));
                    return Progress;
                }
            }
            catch (TaskCanceledException ex)
            {
                Logger.Error(ex, "MediaUpload[{0}] - Task was canceled", UploadUri);
                UpdateProgress(new ResumableUploadProgress(ex, BytesServerReceived));
                throw ex;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "MediaUpload[{0}] - Exception occurred while resuming uploading media", UploadUri);
                UpdateProgress(new ResumableUploadProgress(ex, BytesServerReceived));
                return Progress;
            }

            // Continue to upload the media stream.
            return await UploadCoreAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>The core logic for uploading a stream. It is used by the upload and resume methods.</summary>
        private async Task<IUploadProgress> UploadCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!await SendNextChunkAsync(ContentStream, cancellationToken).ConfigureAwait(false))
                {
                    UpdateProgress(new ResumableUploadProgress(UploadStatus.Uploading, BytesServerReceived));
                }
                UpdateProgress(new ResumableUploadProgress(UploadStatus.Completed, BytesServerReceived));
            }
            catch (TaskCanceledException ex)
            {
                Logger.Error(ex, "MediaUpload[{0}] - Task was canceled", UploadUri);
                UpdateProgress(new ResumableUploadProgress(ex, BytesServerReceived));
                throw ex;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "MediaUpload[{0}] - Exception occurred while uploading media", UploadUri);
                UpdateProgress(new ResumableUploadProgress(ex, BytesServerReceived));
            }

            return Progress;
        }

        /// <summary>
        /// Initiates the resumable upload session and returns the session URI, or upload URI.
        /// See https://developers.google.com/drive/manage-uploads#start-resumable and
        /// https://cloud.google.com/storage/docs/json_api/v1/how-tos/resumable-upload#start-resumable for more information.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// The task containing the session URI to use for the resumable upload.
        /// </returns>
        public abstract Task<Uri> InitiateSessionAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Process a response from the final upload chunk call.
        /// </summary>
        /// <param name="httpResponse">The response body from the final uploaded chunk.</param>
        protected virtual void ProcessResponse(HttpResponseMessage httpResponse)
        {
        }

        /// <summary>Uploads the next chunk of data to the server.</summary>
        /// <returns><c>True</c> if the entire media has been completely uploaded.</returns>
        protected async Task<bool> SendNextChunkAsync(Stream stream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpRequestMessage request = new RequestBuilder()
                {
                    BaseUri = UploadUri,
                    Method = HttpConsts.Put
                }.CreateRequest();
            new ServerErrorCallback(this).AddToRequest(request);

            // Prepare next chunk to send. This is always read into memory one way or another.
            byte[] chunk;
            int chunkLength;
            if (ContentStream.CanSeek)
            {
                PrepareNextChunkKnownSize(stream, cancellationToken, out chunk, out chunkLength);
            }
            else
            {
                PrepareNextChunkUnknownSize(stream, cancellationToken, out chunk, out chunkLength);
            }

            var content = new ByteArrayContent(chunk, 0, chunkLength);
            content.Headers.Add("Content-Range", GetContentRangeHeader(BytesServerReceived, chunkLength));
            request.Content = content;

            BytesClientSent = BytesServerReceived + chunkLength;
            Logger.Debug("MediaUpload[{0}] - Sending bytes={1}-{2}", UploadUri, BytesServerReceived,
                BytesClientSent - 1);

            // We can't assume that the server actually received all the data. It almost always does,
            // but just occasionally it'll return a 308 that makes us resend a chunk. We need to
            // be aware of that so that the upload interceptor is called appropriately afterwards.
            long bytesServerReceivedBefore = BytesServerReceived;
            HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            var completed = await HandleResponse(response).ConfigureAwait(false);
            long bytesServerReceivedAfter = BytesServerReceived;

            int bytesReceivedFromChunk = (int) (bytesServerReceivedAfter - bytesServerReceivedBefore);

            // If we've got an interceptor (e.g. for hashing), we can use it now for as much
            // data as the server actually received.
            if (bytesReceivedFromChunk != 0)
            {
                UploadStreamInterceptor?.Invoke(chunk, 0, bytesReceivedFromChunk);
            }
            return completed;
        }

        /// <summary>Handles a media upload HTTP response.</summary>
        /// <returns><c>True</c> if the entire media has been completely uploaded.</returns>
        private async Task<bool> HandleResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                MediaCompleted(response);
                return true;
            }
            else if (response.StatusCode == (HttpStatusCode)308)
            {
                // The upload protocol uses 308 to indicate that there is more data expected from the server.
                // If the server has received no bytes, it indicates this by not including
                // a Range header in the response..
                var range = response.Headers.FirstOrDefault(x => x.Key.Equals("range", StringComparison.OrdinalIgnoreCase)).Value?.First();
                BytesServerReceived = GetNextByte(range);
                Logger.Debug("MediaUpload[{0}] - {1} Bytes were sent successfully", UploadUri, BytesServerReceived);
                return false;
            }
            throw await ExceptionForResponseAsync(response).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a <see cref="GoogleApiException"/> instance using the error response from the server.
        /// </summary>
        /// <param name="response">The error response.</param>
        /// <returns>An exception which can be thrown by the caller.</returns>
        protected Task<GoogleApiException> ExceptionForResponseAsync(HttpResponseMessage response)
        {
            return MediaApiErrorHandling.ExceptionForResponseAsync(Options?.Serializer, Options?.ServiceName, response);
        }

        /// <summary>A callback when the media was uploaded successfully.</summary>
        private void MediaCompleted(HttpResponseMessage response)
        {
            Logger.Debug("MediaUpload[{0}] - media was uploaded successfully", UploadUri);
            ProcessResponse(response);
            BytesServerReceived = StreamLength;

            // Clear the last request byte array.
            LastMediaRequest = null;
        }

        // TODO: Make this and the next method read the stream using ReadAsync?

        /// <summary>Prepares the given request with the next chunk in case the steam length is unknown.</summary>
        private void PrepareNextChunkUnknownSize(Stream stream, CancellationToken cancellationToken, out byte[] chunk, out int chunkLength)
        {
            if (LastMediaRequest == null)
            {
                // Initialise state
                // ChunkSize + 1 to give room for one extra byte for end-of-stream checking
                LastMediaRequest = new byte[ChunkSize + 1];
                LastMediaLength = 0;
            }
            // Re-use any bytes the server hasn't received
            int copyCount = (int)(BytesClientSent - BytesServerReceived)
                + Math.Max(0, LastMediaLength - ChunkSize);
            if (LastMediaLength != copyCount)
            {
                Buffer.BlockCopy(LastMediaRequest, LastMediaLength - copyCount, LastMediaRequest, 0, copyCount);
                LastMediaLength = copyCount;
            }
            // Read any more required bytes from stream, to form the next chunk
            while (LastMediaLength < ChunkSize + 1 && StreamLength == UnknownSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int readSize = Math.Min(BufferSize, ChunkSize + 1 - LastMediaLength);
                int len = stream.Read(LastMediaRequest, LastMediaLength, readSize);
                LastMediaLength += len;
                if (len == 0)
                {
                    // Stream ended, so we know the length
                    StreamLength = BytesServerReceived + LastMediaLength;
                }
            }

            // If we've read an extra byte, it'll be included in the next chunk.
            chunkLength = Math.Min(ChunkSize, LastMediaLength);
            chunk = LastMediaRequest;
        }

        /// <summary>Prepares the given request with the next chunk in case the steam length is known.</summary>
        private void PrepareNextChunkKnownSize(Stream stream, CancellationToken cancellationToken, out byte[] chunk, out int chunkLength)
        {
            int chunkSize = (int)Math.Min(StreamLength - BytesServerReceived, (long)ChunkSize);

            // Stream length is known and it supports seek and position operations.
            // We can change the stream position and read bytes from the last point.
            // If the number of bytes received by the server isn't equal to the amount of bytes the client sent, we 
            // need to change the position of the input stream, otherwise we can continue from the current position.
            if (stream.Position != BytesServerReceived)
            {
                stream.Position = BytesServerReceived;
            }

            chunk = new byte[chunkSize];
            int bytesLeft = chunkSize;
            int bytesRead = 0;
            while (bytesLeft > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Make sure we only read at most BufferSize bytes at a time.
                int readSize = Math.Min(bytesLeft, BufferSize);
                int len = stream.Read(chunk, bytesRead, readSize);
                if (len == 0)
                {
                    // Presumably the stream lied about its length. Not great, but we still have a chunk to send.
                    break;
                }
                bytesRead += len;
                bytesLeft -= len;
            }
            chunkLength = bytesRead;
        }

        /// <summary>Returns the next byte index need to be sent.</summary>
        private long GetNextByte(string range)
        {
            return range == null ? 0 : long.Parse(range.Substring(range.IndexOf('-') + 1)) + 1;
        }

        /// <summary>
        /// Build a content range header of the form: "bytes X-Y/T" where:
        /// <list type="">
        /// <item>X is the first byte being sent.</item>
        /// <item>Y is the last byte in the range being sent (inclusive).</item>
        /// <item>T is the total number of bytes in the range or * for unknown size.</item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// See: RFC2616 HTTP/1.1, Section 14.16 Header Field Definitions, Content-Range
        /// http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.16
        /// </remarks>
        /// <param name="chunkStart">Start of the chunk.</param>
        /// <param name="chunkSize">Size of the chunk being sent.</param>
        /// <returns>The content range header value.</returns>
        private string GetContentRangeHeader(long chunkStart, long chunkSize)
        {
            string strLength = StreamLength < 0 ? "*" : StreamLength.ToString();

            // If a file of length 0 is sent, one chunk needs to be sent with 0 size.
            // This chunk cannot be specified with the standard (inclusive) range header.
            // In this case, use * to indicate no bytes sent in the Content-Range header.
            if (chunkStart == 0 && chunkSize == 0 && StreamLength == 0)
            {
                return ZeroByteContentRangeHeader;
            }
            else
            {
                long chunkEnd = chunkStart + chunkSize - 1;
                return String.Format("bytes {0}-{1}/{2}", chunkStart, chunkEnd, strLength);
            }
        }

        #endregion Upload Implementation
    }

    /// <summary>
    /// Media upload which uses Google's resumable media upload protocol to upload data.
    /// </summary>
    /// <remarks>
    /// See: https://developers.google.com/drive/manage-uploads#resumable for more information on the protocol.
    /// </remarks>
    /// <typeparam name="TRequest">
    /// The type of the body of this request. Generally this should be the metadata related to the content to be 
    /// uploaded. Must be serializable to/from JSON.
    /// </typeparam>
    public class ResumableUpload<TRequest> : ResumableUpload
    {
        #region Constants

        /// <summary>Payload description headers, describing the content itself.</summary>
        private const string PayloadContentTypeHeader = "X-Upload-Content-Type";

        /// <summary>Payload description headers, describing the content itself.</summary>
        private const string PayloadContentLengthHeader = "X-Upload-Content-Length";

        /// <summary>Specify the type of this upload (this class supports resumable only).</summary>
        private const string UploadType = "uploadType";

        /// <summary>The uploadType parameter value for resumable uploads.</summary>
        private const string Resumable = "resumable";

        #endregion // Constants

        #region Construction

        /// <summary>
        /// Create a resumable upload instance with the required parameters.
        /// </summary>
        /// <param name="service">The client service.</param>
        /// <param name="path">The path for this media upload method.</param>
        /// <param name="httpMethod">The HTTP method to start this upload.</param>
        /// <param name="contentStream">The stream containing the content to upload.</param>
        /// <param name="contentType">Content type of the content to be uploaded. Some services
        /// may allow this to be null; others require a content type to be specified and will
        /// fail when the upload is started if the value is null.</param>
        /// <remarks>
        /// Caller is responsible for maintaining the <paramref name="contentStream"/> open until the upload is 
        /// completed.
        /// Caller is responsible for closing the <paramref name="contentStream"/>.
        /// </remarks>
        protected ResumableUpload(IClientService service, string path, string httpMethod, Stream contentStream, string contentType)
            : base(contentStream,
                   new ResumableUploadOptions
                   {
                       HttpClient = service.HttpClient,
                       Serializer = service.Serializer,
                       ServiceName = service.Name
                   })
        {
            service.ThrowIfNull(nameof(service));
            path.ThrowIfNull(nameof(path));
            httpMethod.ThrowIfNullOrEmpty(nameof(httpMethod));
            contentStream.ThrowIfNull(nameof(contentStream));

            this.Service = service;
            this.Path = path;
            this.HttpMethod = httpMethod;
            this.ContentType = contentType;
        }

        #endregion // Construction

        #region Properties

        /// <summary>Gets or sets the service.</summary>
        public IClientService Service { get; private set; }

        /// <summary>
        /// Gets or sets the path of the method (combined with
        /// <see cref="Google.Apis.Services.IClientService.BaseUri"/>) to produce 
        /// absolute Uri. 
        /// </summary>
        public string Path { get; private set; }

        /// <summary>Gets or sets the HTTP method of this upload (used to initialize the upload).</summary>
        public string HttpMethod { get; private set; }

        /// <summary>Gets or sets the stream's Content-Type.</summary>
        public string ContentType { get; private set; }

        /// <summary>Gets or sets the body of this request.</summary>
        public TRequest Body { get; set; }

        #endregion // Properties

        #region Upload Implementation

        /// <inheritdoc/>
        public override async Task<Uri> InitiateSessionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            HttpRequestMessage request = CreateInitializeRequest();
            Options?.ModifySessionInitiationRequest?.Invoke(request);
            var response = await Service.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw await ExceptionForResponseAsync(response).ConfigureAwait(false);
            }
            return response.Headers.Location;
        }

        /// <summary>Creates a request to initialize a request.</summary>
        private HttpRequestMessage CreateInitializeRequest()
        {
            var builder = new RequestBuilder()
            {
                BaseUri = new Uri(Service.BaseUri),
                Path = Path,
                Method = HttpMethod,
            };

            // init parameters
            builder.AddParameter(RequestParameterType.Query, "key", Service.ApiKey);
            builder.AddParameter(RequestParameterType.Query, UploadType, Resumable);
            SetAllPropertyValues(builder);

            HttpRequestMessage request = builder.CreateRequest();
            if (ContentType != null)
            {
                request.Headers.Add(PayloadContentTypeHeader, ContentType);
            }

            // if the length is unknown at the time of this request, omit "X-Upload-Content-Length" header
            if (ContentStream.CanSeek)
            {
                request.Headers.Add(PayloadContentLengthHeader, StreamLength.ToString());
            }

            Service.SetRequestSerailizedContent(request, Body);
            return request;
        }

        /// <summary>
        /// Reflectively enumerate the properties of this object looking for all properties containing the 
        /// RequestParameterAttribute and copy their values into the request builder.
        /// </summary>
        private void SetAllPropertyValues(RequestBuilder requestBuilder)
        {
            Type myType = this.GetType();
            var properties = myType.GetProperties();

            foreach (var property in properties)
            {
                var attribute = Utilities.GetCustomAttribute<RequestParameterAttribute>(property);

                if (attribute != null)
                {
                    string name = attribute.Name ?? property.Name.ToLower();
                    object value = property.GetValue(this, null);
                    if (value != null)
                    {
                        var valueAsEnumerable = value as IEnumerable;
                        if (!(value is string) && valueAsEnumerable != null)
                        {
                            foreach (var elem in valueAsEnumerable)
                            {
                                requestBuilder.AddParameter(attribute.Type, name, Utilities.ConvertToString(elem));
                            }
                        }
                        else
                        {
                            // Otherwise just convert it to a string.
                            requestBuilder.AddParameter(attribute.Type, name, Utilities.ConvertToString(value));
                        }
                    }
                }
            }
        }

        #endregion Upload Implementation
    }

    /// <summary>
    /// Media upload which uses Google's resumable media upload protocol to upload data.
    /// The version with two types contains both a request object and a response object.
    /// </summary>
    /// <remarks>
    /// See: https://developers.google.com/gdata/docs/resumable_upload for
    /// information on the protocol.
    /// </remarks>
    /// <typeparam name="TRequest">
    /// The type of the body of this request. Generally this should be the metadata related 
    /// to the content to be uploaded. Must be serializable to/from JSON.
    /// </typeparam>
    /// <typeparam name="TResponse">
    /// The type of the response body.
    /// </typeparam>
    public class ResumableUpload<TRequest, TResponse> : ResumableUpload<TRequest>
    {
        #region Construction

        /// <summary>
        /// Create a resumable upload instance with the required parameters.
        /// </summary>
        /// <param name="service">The client service.</param>
        /// <param name="path">The path for this media upload method.</param>
        /// <param name="httpMethod">The HTTP method to start this upload.</param>
        /// <param name="contentStream">The stream containing the content to upload.</param>
        /// <param name="contentType">Content type of the content to be uploaded.</param>
        /// <remarks>
        /// The stream <paramref name="contentStream"/> must support the "Length" property.
        /// Caller is responsible for maintaining the <paramref name="contentStream"/> open until the 
        /// upload is completed.
        /// Caller is responsible for closing the <paramref name="contentStream"/>.
        /// </remarks>
        protected ResumableUpload(IClientService service, string path, string httpMethod,
            Stream contentStream, string contentType)
            : base(service, path, httpMethod, contentStream, contentType) { }

        #endregion // Construction

        #region Properties

        /// <summary>
        /// The response body.
        /// </summary>
        /// <remarks>
        /// This property will be set during upload. The <see cref="ResponseReceived"/> event
        /// is triggered when this has been set.
        /// </remarks>
        public TResponse ResponseBody { get; private set; }

        #endregion // Properties

        #region Events

        /// <summary>Event which is called when the response metadata is processed.</summary>
        public event Action<TResponse> ResponseReceived;

        #endregion // Events

        #region Overrides

        /// <summary>Process the response body </summary>
        protected override void ProcessResponse(HttpResponseMessage response)
        {
            base.ProcessResponse(response);
            ResponseBody = Service.DeserializeResponse<TResponse>(response).Result;

            ResponseReceived?.Invoke(ResponseBody);
        }

        #endregion // Overrides
    }
}
