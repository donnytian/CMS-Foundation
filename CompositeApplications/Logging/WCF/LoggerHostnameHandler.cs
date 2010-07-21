﻿using Composite.WebClient;

namespace Composite.Logging.WCF
{
	internal class LoggerHostnameHandler: Composite.WebClient.WCF.MultipleHostnameHandler
	{
	    private static readonly string ServiceFolderPath = UrlUtils.AdminRootPath + "/services/LogService";
        private static readonly string ServiceFileName = "LogService.svc";
        private static readonly string MappedServiceFolderPath = UrlUtils.AdminRootPath + "/services/LogService/{0}_{1}";

        public LoggerHostnameHandler() : base(ServiceFolderPath, ServiceFileName, MappedServiceFolderPath)
        {
        }
    }
}
