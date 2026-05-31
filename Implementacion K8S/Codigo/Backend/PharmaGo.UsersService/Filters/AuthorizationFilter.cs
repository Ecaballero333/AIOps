using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using InstrumentationInterface;
using PharmaGo.Domain.Entities;
using PharmaGo.IDataAccess;
using PharmaGo.UsersService.IBusinessLogic;

namespace PharmaGo.UsersService.Filters
{
    public class AuthorizationFilter : Attribute, IActionFilter
    {

        private readonly string[] _roles;

        public AuthorizationFilter(string[] roles)
        {
            _roles = roles;
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var _loginManager = GetSessions(context);
            string token = context.HttpContext.Request.Headers["Authorization"];
            try
            {
                if (String.IsNullOrEmpty(token) || !_loginManager.IsTokenValid(token))
                {
                    LogAuthorizationFailure(context, "auth_token_invalid", "validate_token", "Invalid authorization token", null);
                    context.Result = new JsonResult(new { Message = "Invalid authorization token" })
                    { StatusCode = 401 };
                } else if (!_loginManager.IsRoleValid(_roles, token))
                {
                    LogAuthorizationFailure(context, "auth_role_forbidden", "validate_role", "Forbidden role", null);
                    context.Result = new JsonResult(new { Message = "Forbidden role" })
                    { StatusCode = 403 };
                }
                else
                {
                    LogAuthorizationSuccess(context);
                }
            }
            catch (Exception ex)
            {
                LogAuthorizationFailure(context, "auth_db_lookup_fail", "lookup_session_user", "Authorization database lookup failed", ex);
                context.Result = new JsonResult(new { Message = "Invalid authorization token" })
                { StatusCode = 401 };
            }
        }

        private static ILoginManager GetSessions(ActionExecutingContext Context)
        {
            return (ILoginManager)Context.HttpContext.RequestServices.GetService(typeof(ILoginManager));
        }

        private static void LogAuthorizationSuccess(ActionExecutingContext context)
        {
            var logger = GetStructuredLogger(context);
            User? user = GetAuthenticatedUser(context);
            logger?.LogInformation(
                "Authorization succeeded",
                new Dictionary<string, object>
                {
                    ["pharma_biz"] = "auth_success",
                    ["component"] = "AuthorizationFilter",
                    ["operation"] = "authorize_request",
                    ["outcome"] = "success",
                    ["request_path"] = context.HttpContext.Request.Path.Value ?? "unknown",
                    ["user_id"] = user?.Id ?? 0,
                    ["user_name"] = user?.UserName ?? "unknown",
                    ["role"] = user?.Role?.Name ?? "unknown"
                });
        }

        private static User? GetAuthenticatedUser(ActionExecutingContext context)
        {
            try
            {
                string token = context.HttpContext.Request.Headers["Authorization"];
                var guidToken = new Guid(token);
                var sessionRepository = (IRepository<Session>)context.HttpContext.RequestServices.GetService(typeof(IRepository<Session>));
                var userRepository = (IRepository<User>)context.HttpContext.RequestServices.GetService(typeof(IRepository<User>));
                Session session = sessionRepository.GetOneByExpression(x => x.Token == guidToken);
                if (session == null) return null;
                return userRepository.GetOneDetailByExpression(x => x.Id == session.UserId);
            }
            catch
            {
                return null;
            }
        }

        private static void LogAuthorizationFailure(ActionExecutingContext context, string pharmaBiz, string dbOperation, string message, Exception exception)
        {
            var logger = GetStructuredLogger(context);
            var fields = new Dictionary<string, object>
            {
                ["pharma_biz"] = pharmaBiz,
                ["component"] = "AuthorizationFilter",
                ["operation"] = "authorize_request",
                ["outcome"] = "failed",
                ["request_path"] = context.HttpContext.Request.Path.Value ?? "unknown",
                ["error_message"] = exception?.Message ?? message
            };
            if (pharmaBiz == "auth_db_lookup_fail")
            {
                fields["db_operation"] = dbOperation;
                fields["error_type"] = exception?.GetType().Name ?? "unknown";
            }

            if (exception == null)
            {
                logger?.LogWarning(message, null, fields);
            }
            else
            {
                logger?.LogError(message, exception, fields);
            }
        }

        private static IStructuredLogger GetStructuredLogger(ActionExecutingContext context)
        {
            return (IStructuredLogger)context.HttpContext.RequestServices.GetService(typeof(IStructuredLogger));
        }

    }
}
