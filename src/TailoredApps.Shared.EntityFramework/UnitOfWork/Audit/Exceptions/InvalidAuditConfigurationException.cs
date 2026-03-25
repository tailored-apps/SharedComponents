using System;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Exceptions
{
    /// <summary>
    /// Exception thrown when the Unit of Work audit configuration is incomplete or invalid,
    /// e.g. when required properties such as types to collect or entity states are not specified.
    /// </summary>
    [Serializable]
    public class InvalidAuditConfigurationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="InvalidAuditConfigurationException"/>
        /// for the specified missing or invalid configuration property.
        /// </summary>
        /// <param name="propertyName">The name of the misconfigured property.</param>
        public InvalidAuditConfigurationException(string propertyName)
            : base($"Invalid settings for Unit Of Work Audit, missing '${propertyName}` configuration.")
        {
        }
    }
}
