using System.Collections.Generic;

namespace Twino.Mvc.Auth
{
    /// <summary>
    /// Policy Container for Twino MVC
    /// Usually used in Init method to create policies.
    /// After initialization, when a policy name is used in Authorize attribute,
    /// MVC will find the policy by name from this container.
    /// </summary>
    public interface IPolicyContainer
    {

        /// <summary>
        /// Adds new policy to container
        /// </summary>
        void Add(Policy policy);

        /// <summary>
        /// Removes a policy from container by name
        /// </summary>
        void Remove(string name);

        /// <summary>
        /// Clears all registered policies from container
        /// </summary>
        void Clear();

        /// <summary>
        /// Finds a policy by name
        /// </summary>
        Policy Get(string name);

        /// <summary>
        /// Gets all registered policies in container
        /// </summary>
        IEnumerable<Policy> All();

    }
}
