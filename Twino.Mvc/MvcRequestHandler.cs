using Twino.Server;
using Twino.Server.Http;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters;
using Twino.Mvc.Routing;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Serialization;
using Twino.Mvc.Results;
using Twino.Mvc.Errors;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Twino.Mvc.Auth;
using Twino.Core.Http;
using Twino.Mvc.Middlewares;

namespace Twino.Mvc
{
    /// <summary>
    /// HTTP Request Handler implementation of Twino.Server for Twino.Mvc project.
    /// All HTTP Requests starts in here in Request method.
    /// </summary>
    internal class MvcRequestHandler : IHttpRequestHandler
    {
        /// <summary>
        /// Twino.Mvc Facade object
        /// </summary>
        public TwinoMvc Mvc { get; }

        public MvcRequestHandler(TwinoMvc mvc)
        {
            Mvc = mvc;
        }

        /// <summary>
        /// Triggered when a non-websocket request available.
        /// </summary>
        public async Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            try
            {
                if (Mvc.RunnerAction == null)
                {
                    await RequestMvc(server, request, response);
                    return;
                }

                MvcApp app = new MvcApp(request, response);

                Mvc.RunnerAction(app);
                await app.RunSequence();

                if (app.LastResult != null)
                    WriteResponse(response, app.LastResult);
                else
                    await RequestMvc(server, request, response);
            }
            catch (Exception ex)
            {
                if (request.Response.ResponseStream != null)
                    GC.ReRegisterForFinalize(request.Response.ResponseStream);
                
                if (Mvc.IsDevelopment)
                {
                    IErrorHandler handler = new DevelopmentErrorHandler();
                    handler.Error(request, ex);
                }
                else if (Mvc.ErrorHandler != null)
                    Mvc.ErrorHandler.Error(request, ex);
                else
                    WriteResponse(request.Response, StatusCodeResult.InternalServerError());
            }
        }

        /// <summary>
        /// Handles the request in MVC pattern
        /// </summary>
        private async Task RequestMvc(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            //find route
            RouteMatch match = Mvc.RouteFinder.Find(Mvc.Routes, request);
            if (match?.Route == null)
            {
                WriteResponse(response, Mvc.NotFoundResult);
                return;
            }

            //read user token
            ClaimsPrincipal user = null;
            if (Mvc.ClaimsPrincipalValidator != null)
                user = Mvc.ClaimsPrincipalValidator.Get(request);

            FilterContext context = new FilterContext
                                    {
                                        Server = server,
                                        Request = request,
                                        Response = response,
                                        Result = null,
                                        User = user
                                    };

            //check controller authorize attribute
            AuthorizeAttribute[] authController = (AuthorizeAttribute[]) match.Route.ControllerType.GetCustomAttributes(typeof(AuthorizeAttribute), false);
            if (authController.Length > 0)
            {
                authController[0].VerifyAuthority(Mvc, null, context);
                if (context.Result != null)
                {
                    WriteResponse(response, context.Result);
                    return;
                }
            }

            //find controller filters
            IControllerFilter[] controllerFilters = (IControllerFilter[]) match.Route.ControllerType.GetCustomAttributes(typeof(IControllerFilter), true);

            //call BeforeCreated methods of controller attributes
            if (!CallFilters(response, context, controllerFilters, filter => filter.BeforeCreated(context)))
                return;

            TwinoController controller = Mvc.ControllerFactory.CreateInstance(Mvc, match.Route.ControllerType, request, response);
            if (controller == null)
            {
                WriteResponse(response, Mvc.NotFoundResult);
                return;
            }

            controller.User = user;

            //call AfterCreated methods of controller attributes
            if (!CallFilters(response, context, controllerFilters, filter => filter.AfterCreated(controller, context)))
                return;

            //find action filters
            IActionFilter[] actionFilters = (IActionFilter[]) match.Route.ActionType.GetCustomAttributes(typeof(IActionFilter), true);

            //fill action descriptor
            ActionDescriptor descriptor = new ActionDescriptor
                                          {
                                              Controller = controller,
                                              Filters = actionFilters,
                                              Action = match.Route.ActionType,
                                              Parameters = FillParameters(request, match).ToList()
                                          };

            //check action authorize attribute
            AuthorizeAttribute[] authAction = (AuthorizeAttribute[]) match.Route.ActionType.GetCustomAttributes(typeof(AuthorizeAttribute), false);
            if (authAction.Length > 0)
            {
                authAction[0].VerifyAuthority(Mvc, descriptor, context);
                if (context.Result != null)
                {
                    WriteResponse(response, context.Result);
                    return;
                }
            }

            //call BeforeAction methods of controller attributes
            if (!CallFilters(response, context, controllerFilters, filter => filter.BeforeAction(controller, descriptor, context)))
                return;

            //call before action filters
            if (!CallFilters(response, context, actionFilters, filter => filter.Before(controller, descriptor, context)))
                return;

            //execute action
            void Action(IActionResult actionResult)
            {
                if (actionResult == null) return;

                //IActionResult actionResult = match.Route.ActionType.Invoke(controller, descriptor.Parameters.Select(x => x.Value).ToArray()) as IActionResult;
                context.Result = actionResult;

                //call after action filters
                CallFilters(response, context, actionFilters, filter => filter.After(controller, descriptor, actionResult, context), true);

                //call AfterAction methods of controller attributes
                CallFilters(response, context, controllerFilters, filter => filter.AfterAction(controller, descriptor, actionResult, context), true);

                WriteResponse(response, actionResult);
            }

            AsyncStateMachineAttribute a = (AsyncStateMachineAttribute) match.Route.ActionType.GetCustomAttribute(typeof(AsyncStateMachineAttribute));
            if (a == null)
            {
                TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();
                ThreadPool.QueueUserWorkItem(async t =>
                {
                    await Task.Yield();
                    IActionResult ar = (IActionResult) match.Route.ActionType.Invoke(controller, descriptor.Parameters.Select(x => x.Value).ToArray());
                    Action(ar);
                    source.SetResult(true);
                });

                await source.Task;
            }
            else
            {
                Task<IActionResult> task = (Task<IActionResult>) match.Route.ActionType.Invoke(controller, descriptor.Parameters.Select(x => x.Value).ToArray());
                await task;
                Action(task.Result);
            }
        }

        /// <summary>
        /// Writes the action result to the response
        /// </summary>
        public void WriteResponse(HttpResponse response, IActionResult result)
        {
            if (!response.SuppressContentEncoding && result is FileResult)
                response.SuppressContentEncoding = true;

            response.StatusCode = result.Code;
            response.ContentType = result.ContentType;

            if (response.AdditionalHeaders == null)
                response.AdditionalHeaders = result.Headers;
            else
            {
                foreach (var header in result.Headers)
                    if (response.AdditionalHeaders.ContainsKey(header.Key))
                        response.AdditionalHeaders[header.Key] = header.Value;
                    else
                        response.AdditionalHeaders.Add(header.Key, header.Value);
            }

            if (result.Stream != null && result.Stream.Length > 0)
            {
                response.ResponseStream = result.Stream;
                GC.SuppressFinalize(response.ResponseStream);
            }
        }

        /// <summary>
        /// Creates parameter list and sets values for the specified request to the specified route.
        /// </summary>
        private IEnumerable<ParameterValue> FillParameters(HttpRequest request, RouteMatch route)
        {
            foreach (ActionParameter ap in route.Route.Parameters.OrderBy(x => x.Index))
            {
                object value = null;

                //by source find the value of the parameter and set it to "value" local variable
                switch (ap.Source)
                {
                    case ParameterSource.None:
                    case ParameterSource.Route:
                        value = route.Values[ap.FromName];
                        break;

                    case ParameterSource.Body:
                    {
                        string content = Encoding.UTF8.GetString(request.ContentStream.ToArray());
                        if (ap.FromName == "json")
                            value = Newtonsoft.Json.JsonConvert.DeserializeObject(content, ap.ParameterType);
                        else if (ap.FromName == "xml")
                        {
                            using MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
                            XmlSerializer serializer = new XmlSerializer(ap.ParameterType);
                            value = serializer.Deserialize(ms);
                        }

                        break;
                    }

                    case ParameterSource.Form:
                        if (request.Form.ContainsKey(ap.FromName))
                            value = request.Form[ap.FromName];
                        break;

                    case ParameterSource.QueryString:
                        if (request.QueryString.ContainsKey(ap.FromName))
                            value = request.QueryString[ap.FromName];
                        break;

                    case ParameterSource.Header:
                        if (request.Headers.ContainsKey(ap.FromName))
                            value = request.Headers[ap.FromName];
                        break;
                }

                //the value must be cast to the specified parameter type.
                //object boxing may be misleading.
                //when the parameter type of the method is integer, value could be boxed "3" string.
                object casted;

                //if the value is null, parameter may be missing
                if (value == null)
                    casted = null;

                //if the value and parameter types same, do nothing
                else if (value.GetType() == ap.ParameterType)
                    casted = value;

                //need casting. parameter and value types are different
                else
                {
                    //for nullable types, we need extra check (because we need to cast underlying type)
                    if (ap.Nullable)
                    {
                        Type nullable = Nullable.GetUnderlyingType(ap.ParameterType);
                        casted = Convert.ChangeType(value, nullable);
                    }

                    //directly cast
                    else
                        casted = Convert.ChangeType(value, ap.ParameterType);
                }

                //return value
                yield return new ParameterValue
                             {
                                 Name = ap.ParameterName,
                                 Type = ap.ParameterType,
                                 Source = ap.Source,
                                 Value = casted
                             };
            }
        }

        /// <summary>
        /// Calls the action method (from parameter) for the specified response, context for each filter items (from parameter).
        /// Filter parameter action calling is used many times in Request method.
        /// CallFilters method is created to avoid to type this code many times
        /// </summary>
        private bool CallFilters<TFilter>(HttpResponse response, FilterContext context, IEnumerable<TFilter> items, Action<TFilter> action, bool skipResultChanges = false)
        {
            foreach (TFilter item in items)
            {
                action(item);

                if (skipResultChanges)
                    continue;

                if (context.Result != null)
                {
                    WriteResponse(response, context.Result);
                    return false;
                }
            }

            return true;
        }
    }
}