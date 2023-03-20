using System;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Exceptions
{
    [Serializable]
    public class InvalidAuditConfigurationException : Exception
    {
        public InvalidAuditConfigurationException(string propertyName)
            : base($"Invalid settings for Unit Of Work Audit, missing '${propertyName}` configuration.")
        {
        }
    }
}