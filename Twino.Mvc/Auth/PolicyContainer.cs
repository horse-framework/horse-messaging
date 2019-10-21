using System.Collections.Generic;

namespace Twino.Mvc.Auth
{
    /// <summary>
    /// Policy Container for Twino MVC
    /// Usually used in Init method to create policies.
    /// After initialization, when a policy name is used in Authorize attribute,
    /// MVC will find the policy by name from this container.
    /// </summary>
    public class PolicyContainer : IPolicyContainer
    {
        /// <summary>
        /// Registered policy list.
        /// Key is policy name, must be unique
        /// </summary>
        private Dictionary<string, Policy> Policies { get; set; }

        public PolicyContainer()
        {
            Policies = new Dictionary<string, Policy>();
        }

        /// <summary>
        /// Adds new policy to container
        /// </summary>
        public void Add(Policy policy)
        {
            Policies.Add(policy.Name, policy);
        }

        /// <summary>
        /// Gets all registered policies in container
        /// </summary>
        public IEnumerable<Policy> All()
        {
            foreach (Policy policy in Policies.Values)
                yield return policy;
        }

        /// <summary>
        /// Finds a policy by name
        /// </summary>
        public Policy Get(string name)
        {
            if (Policies.ContainsKey(name))
                return Policies[name];

            return null;
        }

        /// <summary>
        /// Removes a policy from container by name
        /// </summary>
        public void Remove(string name)
        {
            Policies.Remove(name);
        }

        /// <summary>
        /// Clears all registered policies from container
        /// </summary>
        public void Clear()
        {
            Policies.Clear();
        }

    }
}
