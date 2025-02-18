// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by google-apis-code-generator 1.5.1
//     C# generator version: 1.40.3
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

/**
 * \brief
 *   Google Play Custom App Publishing API Version v1
 *
 * \section ApiInfo API Version Information
 *    <table>
 *      <tr><th>API
 *          <td><a href='https://developers.google.com/android/work/play/custom-app-api'>Google Play Custom App Publishing API</a>
 *      <tr><th>API Version<td>v1
 *      <tr><th>API Rev<td>20170622 (903)
 *      <tr><th>API Docs
 *          <td><a href='https://developers.google.com/android/work/play/custom-app-api'>
 *              https://developers.google.com/android/work/play/custom-app-api</a>
 *      <tr><th>Discovery Name<td>playcustomapp
 *    </table>
 *
 * \section ForMoreInfo For More Information
 *
 * The complete API documentation for using Google Play Custom App Publishing API can be found at
 * <a href='https://developers.google.com/android/work/play/custom-app-api'>https://developers.google.com/android/work/play/custom-app-api</a>.
 *
 * For more information about the Google APIs Client Library for .NET, see
 * <a href='https://developers.google.com/api-client-library/dotnet/get_started'>
 * https://developers.google.com/api-client-library/dotnet/get_started</a>
 */

namespace Google.Apis.Playcustomapp.v1
{
    /// <summary>The Playcustomapp Service.</summary>
    public class PlaycustomappService : Google.Apis.Services.BaseClientService
    {
        /// <summary>The API version.</summary>
        public const string Version = "v1";

        /// <summary>The discovery version used to generate this service.</summary>
        public static Google.Apis.Discovery.DiscoveryVersion DiscoveryVersionUsed =
            Google.Apis.Discovery.DiscoveryVersion.Version_1_0;

        /// <summary>Constructs a new service.</summary>
        public PlaycustomappService() :
            this(new Google.Apis.Services.BaseClientService.Initializer()) {}

        /// <summary>Constructs a new service.</summary>
        /// <param name="initializer">The service initializer.</param>
        public PlaycustomappService(Google.Apis.Services.BaseClientService.Initializer initializer)
            : base(initializer)
        {
            accounts = new AccountsResource(this);
        }

        /// <summary>Gets the service supported features.</summary>
        public override System.Collections.Generic.IList<string> Features
        {
            get { return new string[0]; }
        }

        /// <summary>Gets the service name.</summary>
        public override string Name
        {
            get { return "playcustomapp"; }
        }

        /// <summary>Gets the service base URI.</summary>
        public override string BaseUri
        {
        #if NETSTANDARD1_3 || NETSTANDARD2_0 || NET45
            get { return BaseUriOverride ?? "https://www.googleapis.com/playcustomapp/v1/accounts/"; }
        #else
            get { return "https://www.googleapis.com/playcustomapp/v1/accounts/"; }
        #endif
        }

        /// <summary>Gets the service base path.</summary>
        public override string BasePath
        {
            get { return "playcustomapp/v1/accounts/"; }
        }

        #if !NET40
        /// <summary>Gets the batch base URI; <c>null</c> if unspecified.</summary>
        public override string BatchUri
        {
            get { return "https://www.googleapis.com/batch/playcustomapp/v1"; }
        }

        /// <summary>Gets the batch base path; <c>null</c> if unspecified.</summary>
        public override string BatchPath
        {
            get { return "batch/playcustomapp/v1"; }
        }
        #endif

        /// <summary>Available OAuth 2.0 scopes for use with the Google Play Custom App Publishing API.</summary>
        public class Scope
        {
            /// <summary>View and manage your Google Play Developer account</summary>
            public static string Androidpublisher = "https://www.googleapis.com/auth/androidpublisher";

        }

        /// <summary>Available OAuth 2.0 scope constants for use with the Google Play Custom App Publishing API.</summary>
        public static class ScopeConstants
        {
            /// <summary>View and manage your Google Play Developer account</summary>
            public const string Androidpublisher = "https://www.googleapis.com/auth/androidpublisher";

        }



        private readonly AccountsResource accounts;

        /// <summary>Gets the Accounts resource.</summary>
        public virtual AccountsResource Accounts
        {
            get { return accounts; }
        }
    }

    ///<summary>A base abstract class for Playcustomapp requests.</summary>
    public abstract class PlaycustomappBaseServiceRequest<TResponse> : Google.Apis.Requests.ClientServiceRequest<TResponse>
    {
        ///<summary>Constructs a new PlaycustomappBaseServiceRequest instance.</summary>
        protected PlaycustomappBaseServiceRequest(Google.Apis.Services.IClientService service)
            : base(service)
        {
        }

        /// <summary>Data format for the response.</summary>
        /// [default: json]
        [Google.Apis.Util.RequestParameterAttribute("alt", Google.Apis.Util.RequestParameterType.Query)]
        public virtual System.Nullable<AltEnum> Alt { get; set; }

        /// <summary>Data format for the response.</summary>
        public enum AltEnum
        {
            /// <summary>Responses with Content-Type of application/json</summary>
            [Google.Apis.Util.StringValueAttribute("json")]
            Json,
        }

        /// <summary>Selector specifying which fields to include in a partial response.</summary>
        [Google.Apis.Util.RequestParameterAttribute("fields", Google.Apis.Util.RequestParameterType.Query)]
        public virtual string Fields { get; set; }

        /// <summary>API key. Your API key identifies your project and provides you with API access, quota, and reports.
        /// Required unless you provide an OAuth 2.0 token.</summary>
        [Google.Apis.Util.RequestParameterAttribute("key", Google.Apis.Util.RequestParameterType.Query)]
        public virtual string Key { get; set; }

        /// <summary>OAuth 2.0 token for the current user.</summary>
        [Google.Apis.Util.RequestParameterAttribute("oauth_token", Google.Apis.Util.RequestParameterType.Query)]
        public virtual string OauthToken { get; set; }

        /// <summary>Returns response with indentations and line breaks.</summary>
        /// [default: true]
        [Google.Apis.Util.RequestParameterAttribute("prettyPrint", Google.Apis.Util.RequestParameterType.Query)]
        public virtual System.Nullable<bool> PrettyPrint { get; set; }

        /// <summary>An opaque string that represents a user for quota purposes. Must not exceed 40
        /// characters.</summary>
        [Google.Apis.Util.RequestParameterAttribute("quotaUser", Google.Apis.Util.RequestParameterType.Query)]
        public virtual string QuotaUser { get; set; }

        /// <summary>Deprecated. Please use quotaUser instead.</summary>
        [Google.Apis.Util.RequestParameterAttribute("userIp", Google.Apis.Util.RequestParameterType.Query)]
        public virtual string UserIp { get; set; }

        /// <summary>Initializes Playcustomapp parameter list.</summary>
        protected override void InitParameters()
        {
            base.InitParameters();

            RequestParameters.Add(
                "alt", new Google.Apis.Discovery.Parameter
                {
                    Name = "alt",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = "json",
                    Pattern = null,
                });
            RequestParameters.Add(
                "fields", new Google.Apis.Discovery.Parameter
                {
                    Name = "fields",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = null,
                    Pattern = null,
                });
            RequestParameters.Add(
                "key", new Google.Apis.Discovery.Parameter
                {
                    Name = "key",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = null,
                    Pattern = null,
                });
            RequestParameters.Add(
                "oauth_token", new Google.Apis.Discovery.Parameter
                {
                    Name = "oauth_token",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = null,
                    Pattern = null,
                });
            RequestParameters.Add(
                "prettyPrint", new Google.Apis.Discovery.Parameter
                {
                    Name = "prettyPrint",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = "true",
                    Pattern = null,
                });
            RequestParameters.Add(
                "quotaUser", new Google.Apis.Discovery.Parameter
                {
                    Name = "quotaUser",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = null,
                    Pattern = null,
                });
            RequestParameters.Add(
                "userIp", new Google.Apis.Discovery.Parameter
                {
                    Name = "userIp",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = null,
                    Pattern = null,
                });
        }
    }

    /// <summary>The "accounts" collection of methods.</summary>
    public class AccountsResource
    {
        private const string Resource = "accounts";

        /// <summary>The service which this resource belongs to.</summary>
        private readonly Google.Apis.Services.IClientService service;

        /// <summary>Constructs a new resource.</summary>
        public AccountsResource(Google.Apis.Services.IClientService service)
        {
            this.service = service;
            customApps = new CustomAppsResource(service);

        }

        private readonly CustomAppsResource customApps;

        /// <summary>Gets the CustomApps resource.</summary>
        public virtual CustomAppsResource CustomApps
        {
            get { return customApps; }
        }

        /// <summary>The "customApps" collection of methods.</summary>
        public class CustomAppsResource
        {
            private const string Resource = "customApps";

            /// <summary>The service which this resource belongs to.</summary>
            private readonly Google.Apis.Services.IClientService service;

            /// <summary>Constructs a new resource.</summary>
            public CustomAppsResource(Google.Apis.Services.IClientService service)
            {
                this.service = service;

            }


            /// <summary>Create and publish a new custom app.</summary>
            /// <param name="body">The body of the request.</param>
            /// <param name="account">Developer account ID.</param>
            public virtual CreateRequest Create(Google.Apis.Playcustomapp.v1.Data.CustomApp body, long account)
            {
                return new CreateRequest(service, body, account);
            }

            /// <summary>Create and publish a new custom app.</summary>
            public class CreateRequest : PlaycustomappBaseServiceRequest<Google.Apis.Playcustomapp.v1.Data.CustomApp>
            {
                /// <summary>Constructs a new Create request.</summary>
                public CreateRequest(Google.Apis.Services.IClientService service, Google.Apis.Playcustomapp.v1.Data.CustomApp body, long account)
                    : base(service)
                {
                    Account = account;
                    Body = body;
                    InitParameters();
                }


                /// <summary>Developer account ID.</summary>
                [Google.Apis.Util.RequestParameterAttribute("account", Google.Apis.Util.RequestParameterType.Path)]
                public virtual long Account { get; private set; }


                /// <summary>Gets or sets the body of this request.</summary>
                Google.Apis.Playcustomapp.v1.Data.CustomApp Body { get; set; }

                ///<summary>Returns the body of the request.</summary>
                protected override object GetBody() { return Body; }

                ///<summary>Gets the method name.</summary>
                public override string MethodName
                {
                    get { return "create"; }
                }

                ///<summary>Gets the HTTP method.</summary>
                public override string HttpMethod
                {
                    get { return "POST"; }
                }

                ///<summary>Gets the REST path.</summary>
                public override string RestPath
                {
                    get { return "{account}/customApps"; }
                }

                /// <summary>Initializes Create parameter list.</summary>
                protected override void InitParameters()
                {
                    base.InitParameters();

                    RequestParameters.Add(
                        "account", new Google.Apis.Discovery.Parameter
                        {
                            Name = "account",
                            IsRequired = true,
                            ParameterType = "path",
                            DefaultValue = null,
                            Pattern = null,
                        });
                }

            }

            /// <summary>Create and publish a new custom app.</summary>
            /// <param name="body">The body of the request.</param>
            /// <param name="account">Developer account ID.</param>
            /// <param name="stream">The stream to upload.</param>
            /// <param name="contentType">The content type of the stream to upload.</param>
            public virtual CreateMediaUpload Create(Google.Apis.Playcustomapp.v1.Data.CustomApp body, long account, System.IO.Stream stream, string contentType)
            {
                return new CreateMediaUpload(service, body, account, stream, contentType);
            }

            /// <summary>Create media upload which supports resumable upload.</summary>
            public class CreateMediaUpload : Google.Apis.Upload.ResumableUpload<Google.Apis.Playcustomapp.v1.Data.CustomApp, Google.Apis.Playcustomapp.v1.Data.CustomApp>
            {

                /// <summary>Data format for the response.</summary>
                /// [default: json]
                [Google.Apis.Util.RequestParameterAttribute("alt", Google.Apis.Util.RequestParameterType.Query)]
                public virtual System.Nullable<AltEnum> Alt { get; set; }

                /// <summary>Data format for the response.</summary>
                public enum AltEnum
                {
                    /// <summary>Responses with Content-Type of application/json</summary>
                    [Google.Apis.Util.StringValueAttribute("json")]
                    Json,
                }

                /// <summary>Selector specifying which fields to include in a partial response.</summary>
                [Google.Apis.Util.RequestParameterAttribute("fields", Google.Apis.Util.RequestParameterType.Query)]
                public virtual string Fields { get; set; }

                /// <summary>API key. Your API key identifies your project and provides you with API access, quota, and
                /// reports. Required unless you provide an OAuth 2.0 token.</summary>
                [Google.Apis.Util.RequestParameterAttribute("key", Google.Apis.Util.RequestParameterType.Query)]
                public virtual string Key { get; set; }

                /// <summary>OAuth 2.0 token for the current user.</summary>
                [Google.Apis.Util.RequestParameterAttribute("oauth_token", Google.Apis.Util.RequestParameterType.Query)]
                public virtual string OauthToken { get; set; }

                /// <summary>Returns response with indentations and line breaks.</summary>
                /// [default: true]
                [Google.Apis.Util.RequestParameterAttribute("prettyPrint", Google.Apis.Util.RequestParameterType.Query)]
                public virtual System.Nullable<bool> PrettyPrint { get; set; }

                /// <summary>An opaque string that represents a user for quota purposes. Must not exceed 40
                /// characters.</summary>
                [Google.Apis.Util.RequestParameterAttribute("quotaUser", Google.Apis.Util.RequestParameterType.Query)]
                public virtual string QuotaUser { get; set; }

                /// <summary>Deprecated. Please use quotaUser instead.</summary>
                [Google.Apis.Util.RequestParameterAttribute("userIp", Google.Apis.Util.RequestParameterType.Query)]
                public virtual string UserIp { get; set; }


                /// <summary>Developer account ID.</summary>
                [Google.Apis.Util.RequestParameterAttribute("account", Google.Apis.Util.RequestParameterType.Path)]
                public virtual long Account { get; private set; }

                /// <summary>Constructs a new Create media upload instance.</summary>
                public CreateMediaUpload(Google.Apis.Services.IClientService service, Google.Apis.Playcustomapp.v1.Data.CustomApp body, long
                 account, System.IO.Stream stream, string contentType)
                    : base(service, string.Format("/{0}/{1}{2}", "upload", service.BasePath, "{account}/customApps"), "POST", stream, contentType)
                {
                    Account = account;
                    Body = body;
                }
            }
        }
    }
}

namespace Google.Apis.Playcustomapp.v1.Data
{    

    /// <summary>This resource represents a custom app.</summary>
    public class CustomApp : Google.Apis.Requests.IDirectResponseSchema
    {
        /// <summary>Default listing language in BCP 47 format.</summary>
        [Newtonsoft.Json.JsonPropertyAttribute("languageCode")]
        public virtual string LanguageCode { get; set; } 

        /// <summary>Title for the Android app.</summary>
        [Newtonsoft.Json.JsonPropertyAttribute("title")]
        public virtual string Title { get; set; } 

        /// <summary>The ETag of the item.</summary>
        public virtual string ETag { get; set; }
    }
}
