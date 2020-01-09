using Twino.Mvc.Controllers;
using Twino.Mvc.Filters;
using System;
using System.Linq;

namespace Twino.Mvc.Auth
{
    /// <summary>
    /// Authorization policy class for Authorize Attributes for Twino MVC
    /// </summary>
    public class Policy
    {
        /// <summary>
        /// Unique Policy Name
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Before the request controller or action processed, if this policy is defined with Authorize attribute,
        /// this function will be called.
        /// If this function returns false, process execution will be canceled and unauthorized response will be sent.
        /// </summary>
        public Func<ActionDescriptor, FilterContext, bool> Func { get; private set; }

        /// <summary>
        /// Creates new policy with an action
        /// </summary>
        public Policy(string name, Func<ActionDescriptor, FilterContext, bool> func)
        {
            Name = name;
            Func = func;
        }

        /// <summary>
        /// Validates if use passed this policy for specified action and filter context.
        /// </summary>
        public bool Validate(ActionDescriptor descriptor, FilterContext context)
        {
            return Func(descriptor, context);
        }

        /// <summary>
        /// Creates custom policy
        /// </summary>
        public static Policy Custom(string name, Func<ActionDescriptor, FilterContext, bool> func)
        {
            return new Policy(name, func);
        }

        /// <summary>
        /// Creates new policy checks if user has specified role or not
        /// </summary>
        public static Policy RequireRole(string name, string role)
        {
            return new Policy(name, (descriptor, context) => context.User.IsInRole(role));
        }

        /// <summary>
        /// Creates new policy checks if user has ONE OF SPECIFIED ROLES.
        /// If user has ONLY ONE ROLE in the list, policy is passed.
        /// </summary>
        public static Policy RequireRoles(string name, params string[] roles)
        {
            return new Policy(name, (descriptor, context) =>
            {
                foreach (string role in roles)
                    if (context.User.IsInRole(role))
                        return true;

                return false;
            });
        }

        /// <summary>
        /// Creates new policy checks if user has specified claim or not
        /// </summary>
        public static Policy RequireClaim(string name, string claim)
        {
            return new Policy(name, (descriptor, context) => context.User.HasClaim(x => x.Type == claim));
        }

        /// <summary>
        /// Creates new policy checks if user has ALL SPECIFIED CLAIMS.
        /// If user MUST HAVE ALL CLAIMS in the list to pass the policy.
        /// </summary>
        public static Policy RequireClaims(string name, params string[] claims)
        {
            return new Policy(name, (descriptor, context) =>
            {
                foreach (string claim in claims)
                    if (!context.User.HasClaim(x => x.Type == claim))
                        return false;

                return true;
            });
        }

        /// <summary>
        /// Creates new policy checks if user has specified claim with specified value or not
        /// </summary>
        public static Policy RequireClaimValue(string name, string claim, string value)
        {
            return new Policy(name, (descriptor, context) => context.User.HasClaim(claim, value));
        }

        /// <summary>
        /// Creates new policy checks if user has specified claim with ONE OF SPECIFIED VALUES or not
        /// If user has ONLY ONE VALUE in the list, policy is passed.
        /// </summary>
        public static Policy RequireClaimValues(string name, string claim, string[] values)
        {
            return new Policy(name, (descriptor, context) =>
            {
                var item = context.User.Claims.FirstOrDefault(x => x.Type == claim);
                if (item == null)
                    return false;

                return values.Contains(item.Value);
            });
        }

    }
}
