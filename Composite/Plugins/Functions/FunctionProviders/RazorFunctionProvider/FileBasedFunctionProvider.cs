﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Composite.Core;
using Composite.Core.IO;
using Composite.Core.Threading;
using Composite.Functions;
using Composite.Functions.Plugins.FunctionProvider;

namespace Composite.Plugins.Functions.FunctionProviders.RazorFunctionProvider
{
    internal abstract class FileBasedFunctionProvider<FunctionType> : IFunctionProvider where FunctionType : FileBasedFunction<FunctionType>
	{
        private static readonly string LogTitle = "FileBasedFunctionProvider";
		private static readonly object _lock = new object();

        private readonly IDictionary<string, FileBasedFunction<FunctionType>> _functionCache = new Dictionary<string, FileBasedFunction<FunctionType>>();

        private readonly C1FileSystemWatcher _watcher;
		private DateTime _lastUpdateTime;
        private readonly string _rootFolder;
        private readonly string _name;

		protected abstract string FileExtension { get; }
		protected abstract Type BaseType { get; }

		public FunctionNotifier FunctionNotifier { private get; set; }
		public string VirtualPath { get; private set; }
		public string PhysicalPath { get; private set; }

		public IEnumerable<IFunction> Functions
		{
			get
			{
                var returnList = new List<FileBasedFunction<FunctionType>>();

				var files = new C1DirectoryInfo(PhysicalPath)
                    .GetFiles("*." + FileExtension, SearchOption.AllDirectories)
                    .Where(f => !f.Name.StartsWith("_", StringComparison.Ordinal));

				foreach (var file in files)
				{
                    string ns = ExtractFuctionNamespace(file.FullName);
                    string name = Path.GetFileNameWithoutExtension(file.Name);

					var virtualPath = Path.Combine(VirtualPath, 
                                                   ns.Replace(".", Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture)), 
                                                   name + "." + FileExtension);
					object obj = null;

					try
					{
						obj = InstantiateFile(virtualPath);
					}
					catch (Exception exc)
					{
                        Log.LogError(LogTitle, "Error instantiating {0} function", _name);
                        Log.LogError(LogTitle, exc);

						if (_functionCache.ContainsKey(virtualPath))
						{
							returnList.Add(_functionCache[virtualPath]);
						}

						continue;
					}

					var parameters = GetParameters(obj);
					var returnType = GetReturnType(obj);
					var description = GetDescription(obj);
                    var function = (FileBasedFunction<FunctionType>)typeof(FunctionType)
                        .GetConstructors().First()
                        .Invoke(new object[] { ns, name, description, parameters, returnType, virtualPath, this });

					_functionCache[virtualPath] = function;

					returnList.Add(function);
				}

				return returnList;
			}
		}

        private string ExtractFuctionNamespace(string filePath)
        {
            var parts = filePath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            string ns = String.Empty;

            for (int i = parts.Length - 2; i > 0; i--)
            {
                if (parts[i].Equals(_rootFolder, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                ns = parts[i] + "." + ns;
            }

            return ns.Substring(0, ns.Length - 1);
        }

		public FileBasedFunctionProvider(string name, string folder)
		{
			_name = name;

			VirtualPath = folder;
			PhysicalPath = PathUtil.Resolve(VirtualPath);

			_rootFolder = PhysicalPath.Split(new[] { Path.DirectorySeparatorChar }).Last();

			_watcher = new C1FileSystemWatcher(PhysicalPath, "*")
			{
				IncludeSubdirectories = true
			};

            _watcher.Created += Watcher_OnChanged;
            _watcher.Deleted += Watcher_OnChanged;
            _watcher.Changed += Watcher_OnChanged;
            _watcher.Renamed += Watcher_OnChanged;

			_watcher.EnableRaisingEvents = true;
		}

		protected abstract Type GetReturnType(object obj);
		protected abstract object InstantiateFile(string virtualPath);
		protected abstract bool HandleChange(string path);

		private IDictionary<string, FunctionParameterHolder> GetParameters(object obj)
		{
			var dict = new Dictionary<string, FunctionParameterHolder>();

			var type = obj.GetType();
			while (type != BaseType)
			{
				var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.DeclaredOnly);
				foreach (var prop in properties)
				{
					var propType = prop.PropertyType;
					var name = prop.Name;
					var att = prop.GetCustomAttributes(typeof(FunctionParameterAttribute), false).Cast<FunctionParameterAttribute>().FirstOrDefault();
					WidgetFunctionProvider widgetProvider = null;

					if (att != null && !String.IsNullOrEmpty(att.WidgetMarkup))
					{
						var el = XElement.Parse(att.WidgetMarkup);

						widgetProvider = new WidgetFunctionProvider(el);
					}

					if (!dict.ContainsKey(name))
					{
						dict.Add(name, new FunctionParameterHolder(name, propType, att, widgetProvider));
					}
				}

				type = type.BaseType;
			}

			return dict;
		}

		private string GetDescription(object obj)
		{
			var attr = obj.GetType().GetCustomAttributes(typeof(FunctionAttribute), false).Cast<FunctionAttribute>().FirstOrDefault();
			if (attr != null)
			{
				return attr.Description;
			}

			return String.Format("A {0} function", _name);
		}

		private void Watcher_OnChanged(object sender, FileSystemEventArgs e)
		{
			if (FunctionNotifier != null && HandleChange(e.FullPath))
			{
				lock (_lock)
				{
					var timeSpan = DateTime.Now - _lastUpdateTime;
					if (timeSpan.TotalMilliseconds > 100)
					{
 					    using (ThreadDataManager.EnsureInitialize())
						{
							FunctionNotifier.FunctionsUpdated();
						}

						_lastUpdateTime = DateTime.Now;
					}
				}
			}
		}
	}
}