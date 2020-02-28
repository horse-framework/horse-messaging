using System;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters;
using Twino.Mvc.Results;

namespace Twino.Mvc.Auth
{
    /// <summary>
    /// Authorization attribute for Twino MVC.
    /// Can be used for controllers and actions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeAttribute : Attribute
    {
        /// <summary>
        /// Required role list.
        /// User should have AT LEAST one role
        /// </summary>
        public string Roles { get; set; }

        /// <summary>
        /// Required claim list.
        /// User should have ALL claims
        /// </summary>
        public string Claims { get; set; }

        /// <summary>
        /// Policy name for authorization
        /// </summary>
        public string Policy { get; set; }

        /// <summary>
        /// Creates new default authorize attribute
        /// </summary>
        public AuthorizeAttribute()
        {
        }

        /// <summary>
        /// Creates new authorize attribute with policy name
        /// </summary>
        public AuthorizeAttribute(string policy)
        {
            Policy = policy;
        }

        /// <summary>
        /// Verifies authority of action execution.
        /// If authorization fails, context.Result will be set to 403 or 401
        /// </summary>
        public void VerifyAuthority(TwinoMvc mvc, ActionDescriptor descriptor, FilterContext context)
        {
            if (context.User == null)
            {
                context.Result = StatusCodeResult.Unauthorized();
                return;
            }

            if (!CheckPolicy(mvc, descriptor, context))
                return;

            if (!CheckRoles(descriptor, context))
                return;

            CheckClaims(descriptor, context);
        }

        /// <summary>
        /// Checks Policy property.
        /// If it's not empty and validation fails, return false.
        /// Otherwise returns true.
        /// </summary>
        private bool CheckPolicy(TwinoMvc mvc, ActionDescriptor descriptor, FilterContext context)
        {
            if (string.IsNullOrEmpty(Policy))
                return true;

            Policy policy = mvc.Policies.Get(Policy);
            if (policy != null)
            {
                if (!policy.Validate(descriptor, context))
                {
                    context.Result = StatusCodeResult.Unauthorized();
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks Roles property.
        /// If user doesn't have ANY roles in Roles property returns false
        /// Otherwise returns true.
        /// </summary>
        private bool CheckRoles(ActionDescriptor descriptor, FilterContext context)
        {
            if (string.IsNullOrEmpty(Roles))
                return true;

            string[] roles = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (string role in roles)
                if (!context.User.IsInRole(role))
                    return false;

            return true;
        }

        /// <summary>
        /// Checks Claims property.
        /// If user doesn't have ALL claims in Roles property returns false
        /// Otherwise returns true.
        /// </summary>
        private bool CheckClaims(ActionDescriptor descriptor, FilterContext context)
        {
            if (string.IsNullOrEmpty(Claims))
                return true;

            string[] claims = Claims.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (string claim in claims)
            {
                if (claim.Contains('='))
                {
                    string[] key_value = claim.Split('=');
                    if (!context.User.HasClaim(key_value[0], key_value[1]))
                        return false;
                }
                else if (!context.User.HasClaim(x => x.Type == claim))
                    return false;
            }

            return true;
        }

    }
}
