using System;
using System.Collections.Generic;
using System.Net;
using PayPal.Log;
using System.Text;
using System.Threading;

namespace PayPal.Api
{
    /// <summary>
    /// Abstract class that handles configuring an HTTP request prior to making an API call.
    /// </summary>
    public abstract class PayPalResource : PayPalSerializableObject
    {
        /// <summary>
        /// Logs output statements, errors, debug info to a text file
        /// </summary>
        private static Logger logger = Logger.GetLogger(typeof(PayPalResource));

        /// <summary>
        /// PayPal debug id from response header
        /// </summary>
        private String _debugId;
        /// <summary>
        /// Sets the PayPal debug id from response header
        /// </summary>
        protected void SetDebugId(string debugId)
        {
            _debugId = debugId;
        }
        /// <summary>
        /// Gets the PayPal debug id of the last request from the response header. 
        //  The debug id value can change if additional API operations are performed on the object. 
        /// </summary>
        public String GetDebugId()
        {
            return _debugId;
        }

        /// <summary>
        /// List of supported HTTP methods when making HTTP requests to the PayPal REST API.
        /// </summary>
        protected internal enum HttpMethod
        {
            /// <summary>
            /// GET HTTP request. This is typically used in API operations to retrieve a static resource.
            /// </summary>
            GET,

            /// <summary>
            /// HEAD HTTP request. This is typically used to retrieve only the header information for a static resource.
            /// </summary>
            HEAD,

            /// <summary>
            /// POST HTTP request. This is typically used in API operations that require data in the request body to complete.
            /// </summary>
            POST,

            /// <summary>
            /// PUT HTTP request. This is used in some API operations that update a given resource.
            /// </summary>
            PUT,

            /// <summary>
            /// DELETE HTTP request. This is typcially used in API oeprations that delete a given resource.
            /// </summary>
            DELETE,

            /// <summary>
            /// PATCH HTTP request. This is typcially used in API operations that update a given resource.
            /// </summary>
            PATCH
        }

        /// <summary>
        /// Gets the last request sent by the SDK in the current thread.
        /// </summary>
        public static ThreadLocal<RequestDetails> LastRequestDetails { get; private set; }

        /// <summary>
        /// Gets the last response received by the SDK in the current thread.
        /// </summary>
        public static ThreadLocal<ResponseDetails> LastResponseDetails { get; private set; }

        /// <summary>
        /// Static constructor initializing any static properties.
        /// </summary>
        static PayPalResource()
        {
            LastRequestDetails = new ThreadLocal<RequestDetails>();
            LastResponseDetails = new ThreadLocal<ResponseDetails>();
        }

        /// <summary>
        /// Configures and executes REST call: Supports JSON
        /// </summary>
        /// <param name="apiContext">APIContext object</param>
        /// <param name="httpMethod">HttpMethod type</param>
        /// <param name="resource">URI path of the resource</param>
        /// <param name="payload">JSON request payload</param>
        /// <param name="endpoint">Optional endpoint to use when generating the full URL for the resource. If none is specified, a default endpoint is generated by the SDK based on other config settings.</param>
        /// <param name="setAuthorizationHeader">Specifies whether or not to set the Authorization header in outgoing requests. Defaults to true.</param>
        /// <returns>Response object or null otherwise for void API calls</returns>
        /// <exception cref="PayPal.HttpException">Thrown if there was an error sending the request.</exception>
        /// <exception cref="PayPal.PaymentsException">Thrown if an HttpException was raised and contains a Payments API error object.</exception>
        /// <exception cref="PayPal.PayPalException">Thrown for any other issues encountered. See inner exception for further details.</exception>
        protected internal static object ConfigureAndExecute(APIContext apiContext, HttpMethod httpMethod, string resource, string payload = "", string endpoint = "", bool setAuthorizationHeader = true)
        {
            return ConfigureAndExecute<object>(apiContext, httpMethod, resource, payload, endpoint, setAuthorizationHeader);
        }

        /// <summary>
        /// Configures and executes REST call: Supports JSON
        /// </summary>
        /// <typeparam name="T">Generic Type parameter for response object</typeparam>
        /// <param name="apiContext">APIContext object</param>
        /// <param name="httpMethod">HttpMethod type</param>
        /// <param name="resource">URI path of the resource</param>
        /// <param name="payload">JSON request payload</param>
        /// <param name="endpoint">Endpoint to use when generating the full URL for the resource. If none is specified, a default endpoint is generated by the SDK based on other config settings.</param>
        /// <param name="setAuthorizationHeader">Specifies whether or not to set the Authorization header in outgoing requests. Defaults to true.</param>
        /// <returns>Response object or null otherwise for void API calls</returns>
        /// <exception cref="PayPal.HttpException">Thrown if there was an error sending the request.</exception>
        /// <exception cref="PayPal.PaymentsException">Thrown if an HttpException was raised and contains a Payments API error object.</exception>
        /// <exception cref="PayPal.PayPalException">Thrown for any other issues encountered. See inner exception for further details.</exception>
        protected internal static T ConfigureAndExecute<T>(APIContext apiContext, HttpMethod httpMethod, string resource, string payload = "", string endpoint = "", bool setAuthorizationHeader = true)
        {
            // Verify the state of the APIContext object.
            if (apiContext == null)
            {
                throw new PayPalException("APIContext object is null");
            }

            try
            {
                var config = apiContext.GetConfigWithDefaults();
                var headersMap = GetHeaderMap(apiContext);

                if(!setAuthorizationHeader && headersMap.ContainsKey(BaseConstants.AuthorizationHeader))
                {
                    headersMap.Remove(BaseConstants.AuthorizationHeader);
                }

                if (string.IsNullOrEmpty(endpoint))
                {
                    endpoint = GetEndpoint(config);
                }

                // Create the URI where the HTTP request will be sent.
                Uri uniformResourceIdentifier = null;
                var baseUri = new Uri(endpoint);
                if (!Uri.TryCreate(baseUri, resource, out uniformResourceIdentifier))
                {
                    throw new PayPalException("Cannot create URL; baseURI=" + baseUri + ", resourcePath=" + resource);
                }

                // Create the HttpRequest object that will be used to send the HTTP request.
                var connMngr = ConnectionManager.Instance;
                var httpRequest = connMngr.GetConnection(config, uniformResourceIdentifier.ToString());
                httpRequest.Method = httpMethod.ToString();

                // Set custom content type (default to [application/json])
                if (headersMap != null && headersMap.ContainsKey(BaseConstants.ContentTypeHeader))
                {
                    httpRequest.ContentType = headersMap[BaseConstants.ContentTypeHeader].Trim();
                    headersMap.Remove(BaseConstants.ContentTypeHeader);
                }
                else
                {
                    httpRequest.ContentType = BaseConstants.ContentTypeHeaderJson;
                }

                // Set User-Agent HTTP header
                if (headersMap.ContainsKey(BaseConstants.UserAgentHeader))
                {
                    // aganzha
                    //iso-8859-1
                    var iso8851 = Encoding.GetEncoding("iso-8859-1", new EncoderReplacementFallback(string.Empty), new DecoderExceptionFallback());
                    var bytes = Encoding.Convert(Encoding.UTF8, iso8851, Encoding.UTF8.GetBytes(headersMap[BaseConstants.UserAgentHeader]));
                    httpRequest.Headers["User-Agent"] = iso8851.GetString(bytes);
                    //httpRequest.UserAgent = iso8851.GetString(bytes);
                    headersMap.Remove(BaseConstants.UserAgentHeader);
                }

                // Set Custom HTTP headers
                foreach (KeyValuePair<string, string> entry in headersMap)
                {
                    httpRequest.Headers[entry.Key] = entry.Value;
                    //httpRequest.Headers.Add(entry.Key, entry.Value);
                }

                // Log the headers
                foreach (string headerName in httpRequest.Headers)
                {
                    logger.DebugFormat(headerName + ":" + httpRequest.Headers[headerName]);
                }

                // Execute call
                var connectionHttp = new HttpConnection(config);

                // Setup the last request & response details.
                LastRequestDetails.Value = connectionHttp.RequestDetails;
                LastResponseDetails.Value = connectionHttp.ResponseDetails;

                var response = (connectionHttp.Execute(payload, httpRequest)).Result;

                if (typeof(T).Name.Equals("Object"))
                {
                    return default(T);
                }
                else if (typeof(T).Name.Equals("String"))
                {
                    return (T)Convert.ChangeType(response, typeof(T));
                }

                var formattedResponse = JsonFormatter.ConvertFromJson<T>(response);
                if (formattedResponse is PayPalResource)
                {
                    var responseHeaders = connectionHttp.ResponseDetails.Headers;
                    String debugId = responseHeaders["PayPal-Debug-Id"];
                    //String debugId = responseHeaders.Get("PayPal-Debug-Id");
                    ((PayPalResource)(object)formattedResponse).SetDebugId(debugId);

                }

                return formattedResponse;
            }
            catch (ConnectionException ex)
            {
                if (ex is HttpException)
                {
                    var httpEx = ex as HttpException;
                    //  Check to see if we have a Payments API error.
                    if (httpEx.StatusCode == HttpStatusCode.BadRequest)
                    {
                        PaymentsException paymentsEx;
                        if (httpEx.TryConvertTo<PaymentsException>(out paymentsEx))
                        {
                            throw paymentsEx;
                        }
                    }
                    else if (httpEx.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        IdentityException identityEx;
                        if (httpEx.TryConvertTo<IdentityException>(out identityEx))
                        {
                            throw identityEx;
                        }
                    }
                }
                throw;
            }
            catch (PayPalException)
            {
                // If we get a PayPalException, just rethrow to preserve the stack trace.
                throw;
            }
            catch (System.Exception ex)
            {
                throw new PayPalException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Gets a collection of headers to be used in an HTTP request.
        /// </summary>
        /// <param name="apiContext">APIContext object containing information needed to construct the headers map.</param>
        /// <returns>A collection of headers.</returns>
        public static Dictionary<string, string> GetHeaderMap(APIContext apiContext)
        {
            var headers = new Dictionary<string, string>();

		    // The implementation is PayPal specific. The Authorization header is
		    // formed for OAuth or Basic, for OAuth system the authorization token
		    // passed as a parameter is used in creation of HTTP header, for Basic
		    // Authorization the ClientID and ClientSecret passed as parameters are
		    // used after a Base64 encoding.
            if (!string.IsNullOrEmpty(apiContext.AccessToken))
            {
                headers[BaseConstants.AuthorizationHeader] = apiContext.AccessToken;
            }
            else
            {
                var config = apiContext.GetConfigWithDefaults();
                var clientId = config.ContainsKey(BaseConstants.ClientId) ? config[BaseConstants.ClientId] : null;
                var clientSecret = config.ContainsKey(BaseConstants.ClientSecret) ? config[BaseConstants.ClientSecret] : null;
                var encodedCredentials = EncodeToBase64(clientId, clientSecret);
                headers[BaseConstants.AuthorizationHeader] = "Basic " + encodedCredentials;
            }

            // Appends request Id which is used by PayPal API service for
            // idempotency
            if (!apiContext.MaskRequestId && !string.IsNullOrEmpty(apiContext.RequestId))
            {
                headers[BaseConstants.PayPalRequestIdHeader] = apiContext.RequestId;
            }

            // Add User-Agent header for tracking in PayPal system
            var userAgentMap = UserAgentHeader.GetHeader();
            if (userAgentMap != null && userAgentMap.Count > 0)
            {
                foreach (KeyValuePair<string, string> entry in userAgentMap)
                {
                    headers[entry.Key] = entry.Value;
                }
            }

            // Add any custom headers
            if (apiContext.HTTPHeaders != null && apiContext.HTTPHeaders.Count > 0)
            {
                foreach (var header in apiContext.HTTPHeaders)
                {
                    headers[header.Key] = header.Value;
                }
            }
            return headers;
        }

        /// <summary>
        /// Gets the endpoint to be used when making an HTTP call to the REST API.
        /// </summary>
        /// <returns>The endpoint to be used when making an HTTP call to the REST API.</returns>
        public static string GetEndpoint(Dictionary<string, string> config)
        {
            string endpoint = null;

            // Try and load the endpoint from the config.
            if (config.ContainsKey(BaseConstants.EndpointConfig))
            {
                endpoint = config[BaseConstants.EndpointConfig];
            }
            else if (config.ContainsKey(BaseConstants.ApplicationModeConfig))
            {
                switch (config[BaseConstants.ApplicationModeConfig])
                {
                    case BaseConstants.LiveMode:
                        endpoint = BaseConstants.RESTLiveEndpoint;
                        break;
                    case BaseConstants.SandboxMode:
                        endpoint = BaseConstants.RESTSandboxEndpoint;
                        break;
                    case BaseConstants.SecurityTestSandboxMode:
                        endpoint = BaseConstants.RESTSecurityTestSandoxEndpoint;
                        break;
                }
            }

            // If no endpoint is defined, then default to sandbox.
            if (string.IsNullOrEmpty(endpoint))
            {
                endpoint = BaseConstants.RESTSandboxEndpoint;
            }

            if (!endpoint.EndsWith("/"))
            {
                endpoint += "/";
            }

            return endpoint;
        }

        /// <summary>
        /// Covnerts the specified client credentials to a base-64 string for authorization purposes.
        /// </summary>
        /// <param name="clientId">The client ID to be used in generating the base-64 client identifier.</param>
        /// <param name="clientSecret">The client secret to be used in generating the base-64 client identifier.</param>
        /// <returns>The base-64 encoded client identifier to use in the authorization request.</returns>
        /// <exception cref="PayPal.MissingCredentialException">Thrown if clientId or clientSecret are null or empty.</exception>
        /// <exception cref="PayPal.InvalidCredentialException">Thrown if there is an issue converting the credentials to a formatted authorization string.</exception>
        /// <exception cref="PayPal.PayPalException">Thrown for any other issue encountered. See inner exception for further details.</exception>
        private static string EncodeToBase64(string clientId, string clientSecret)
        {
            // Validate the provided credentials. If either value is null or empty, then throw.
            if (string.IsNullOrEmpty(clientId))
            {
                throw new MissingCredentialException("clientId is missing.");
            }
            else if (string.IsNullOrEmpty(clientSecret))
            {
                throw new MissingCredentialException("clientSecret is missing.");
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(string.Format("{0}:{1}", clientId, clientSecret));
                return Convert.ToBase64String(bytes);
            }
            catch (System.Exception ex)
            {
                if (ex is FormatException || ex is ArgumentNullException)
                {
                    throw new InvalidCredentialException("Unable to convert client credentials to base-64 string.\n" +
                                                         "  clientId: \"" + clientId + "\"\n" +
                                                         "  clientSecret: \"" + clientSecret + "\"\n" +
                                                         "  Error: " + ex.Message);
                }

                throw new PayPalException(ex.Message, ex);
            }
        }
    }
}
