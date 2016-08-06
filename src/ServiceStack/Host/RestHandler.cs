using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using ServiceStack.Host.Handlers;
using ServiceStack.MiniProfiler;
using ServiceStack.Text;
using ServiceStack.Web;

namespace ServiceStack.Host
{
    public class RestHandler
        : ServiceStackHandlerBase
    {
        public RestHandler()
        {
            this.HandlerAttributes = RequestAttributes.Reply;
        }

        public static IRestPath FindMatchingRestPath(string httpMethod, string pathInfo, out string contentType)
        {
            pathInfo = GetSanitizedPathInfo(pathInfo, out contentType);

            return HostContext.ServiceController.GetRestPathForRequest(httpMethod, pathInfo);
        }

        public static string GetSanitizedPathInfo(string pathInfo, out string contentType)
        {
            contentType = null;
            if (HostContext.Config.AllowRouteContentTypeExtensions)
            {
                var pos = pathInfo.LastIndexOf('.');
                if (pos >= 0)
                {
                    var format = pathInfo.Substring(pos + 1);
                    contentType = HostContext.ContentTypes.GetFormatContentType(format);
                    if (contentType != null)
                    {
                        pathInfo = pathInfo.Substring(0, pos);
                    }
                }
            }
            return pathInfo;
        }

        public IRestPath GetRestPath(string httpMethod, string pathInfo)
        {
            if (this.RestPath == null)
            {
                string contentType;
                this.RestPath = FindMatchingRestPath(httpMethod, pathInfo, out contentType);

                if (contentType != null)
                    ResponseContentType = contentType;
            }
            return this.RestPath;
        }

        public IRestPath RestPath { get; set; }

        // Set from SSHHF.GetHandlerForPathInfo()
        public string ResponseContentType { get; set; }

        public override bool RunAsAsync()
        {
            return true;
        }

        public override async Task ProcessRequestAsync(IRequest httpReq, IResponse httpRes, string operationName)
        {
            try
            {
                var appHost = HostContext.AppHost;
                if (appHost.ApplyPreRequestFilters(httpReq, httpRes))
                    return;

                var restPath = GetRestPath(httpReq.Verb, httpReq.PathInfo);
                if (restPath == null)
                {
                    throw new NotSupportedException("No RestPath found for: " + httpReq.Verb + " " + httpReq.PathInfo);
                }
                httpReq.SetRoute(restPath as RestPath);

                operationName = restPath.RequestType.GetOperationName();

                var callback = httpReq.GetJsonpCallback();
                var doJsonp = HostContext.Config.AllowJsonpRequests
                              && !string.IsNullOrEmpty(callback);

                if (ResponseContentType != null)
                    httpReq.ResponseContentType = ResponseContentType;

                var responseContentType = httpReq.ResponseContentType;
                appHost.AssertContentType(responseContentType);

                var request = httpReq.Dto = CreateRequest(httpReq, restPath);

                if (appHost.ApplyRequestFilters(httpReq, httpRes, request))
                    return;

                var rawResponse = GetResponse(httpReq, request);

                if (httpRes.IsClosed)
                    return;

                var response = await HandleResponseAsync(rawResponse).ConfigureAwait(false);

                response = appHost.ApplyResponseConverters(httpReq, response);

                if (appHost.ApplyResponseFilters(httpReq, httpRes, response))
                    return;

                if (responseContentType.Contains("jsv") && !string.IsNullOrEmpty(httpReq.QueryString[Keywords.Debug]))
                    await WriteDebugResponse(httpRes, response).ConfigureAwait(false);

                if (doJsonp && !(response is CompressedResult))
                    await httpRes.WriteToResponse(httpReq, response, (callback + "(").ToUtf8Bytes(), ")".ToUtf8Bytes()).ConfigureAwait(false);

                await httpRes.WriteToResponse(httpReq, response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (HostContext.Config.WriteErrorsToResponse)
                {
                    var task = HandleException(httpReq, httpRes, operationName, ex.ApplyResponseConverters(httpReq));
                    Task.WaitAll(task);
                    return;
                }
                throw ex.ApplyResponseConverters(httpReq);
            }
        }

        public override object GetResponse(IRequest request, object requestDto)
        {
            var requestContentType = ContentFormat.GetEndpointAttributes(request.ResponseContentType);

            request.RequestAttributes |= HandlerAttributes | requestContentType;

            return ExecuteService(requestDto, request);
        }

        public static object CreateRequest(IRequest httpReq, IRestPath restPath)
        {
            var dtoFromBinder = GetCustomRequestFromBinder(httpReq, restPath.RequestType);
            if (dtoFromBinder != null)
                return HostContext.AppHost.ApplyRequestConverters(httpReq, dtoFromBinder);

            var requestParams = httpReq.GetFlattenedRequestParams();
            return HostContext.AppHost.ApplyRequestConverters(httpReq,
                CreateRequest(httpReq, restPath, requestParams));
        }

        public static object CreateRequest(IRequest httpReq, IRestPath restPath, Dictionary<string, string> requestParams)
        {
            var requestDto = CreateContentTypeRequest(httpReq, restPath.RequestType, httpReq.ContentType);

            return CreateRequest(httpReq, restPath, requestParams, requestDto);
        }

        public static object CreateRequest(IRequest httpReq, IRestPath restPath, Dictionary<string, string> requestParams, object requestDto)
        {
            string contentType;
            var pathInfo = !restPath.IsWildCardPath
                ? GetSanitizedPathInfo(httpReq.PathInfo, out contentType)
                : httpReq.PathInfo;

            return restPath.CreateRequest(pathInfo, requestParams, requestDto);
        }

        /// <summary>
        /// Used in Unit tests
        /// </summary>
        /// <returns></returns>
        public override object CreateRequest(IRequest httpReq, string operationName)
        {
            if (this.RestPath == null)
                throw new ArgumentNullException("No RestPath found");

            return CreateRequest(httpReq, this.RestPath);
        }
    }

}
