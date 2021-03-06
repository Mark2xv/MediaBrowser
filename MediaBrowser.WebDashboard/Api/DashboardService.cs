﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.IO;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using ServiceStack.ServiceHost;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.WebDashboard.Api
{
    /// <summary>
    /// Class GetDashboardConfigurationPages
    /// </summary>
    [Route("/dashboard/ConfigurationPages", "GET")]
    [Restrict(VisibilityTo = EndpointAttributes.None)]
    public class GetDashboardConfigurationPages : IReturn<List<ConfigurationPageInfo>>
    {
        /// <summary>
        /// Gets or sets the type of the page.
        /// </summary>
        /// <value>The type of the page.</value>
        public ConfigurationPageType? PageType { get; set; }
    }

    /// <summary>
    /// Class GetDashboardConfigurationPage
    /// </summary>
    [Route("/dashboard/ConfigurationPage", "GET")]
    [Restrict(VisibilityTo = EndpointAttributes.None)]
    public class GetDashboardConfigurationPage
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }
    }

    /// <summary>
    /// Class GetDashboardResource
    /// </summary>
    [Route("/dashboard/{ResourceName*}", "GET")]
    [Restrict(VisibilityTo = EndpointAttributes.None)]
    public class GetDashboardResource
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string ResourceName { get; set; }
        /// <summary>
        /// Gets or sets the V.
        /// </summary>
        /// <value>The V.</value>
        public string V { get; set; }
    }

    /// <summary>
    /// Class GetDashboardInfo
    /// </summary>
    [Route("/dashboard/dashboardInfo", "GET")]
    [Restrict(VisibilityTo = EndpointAttributes.None)]
    public class GetDashboardInfo : IReturn<DashboardInfo>
    {
    }

    /// <summary>
    /// Class DashboardService
    /// </summary>
    [Export(typeof(IRestfulService))]
    public class DashboardService : IRestfulService, IHasResultFactory
    {
        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Gets or sets the HTTP result factory.
        /// </summary>
        /// <value>The HTTP result factory.</value>
        public IHttpResultFactory ResultFactory { get; set; }

        /// <summary>
        /// Gets or sets the request context.
        /// </summary>
        /// <value>The request context.</value>
        public IRequestContext RequestContext { get; set; }

        /// <summary>
        /// Gets or sets the task manager.
        /// </summary>
        /// <value>The task manager.</value>
        private readonly ITaskManager _taskManager;

        /// <summary>
        /// The _user manager
        /// </summary>
        private readonly IUserManager _userManager;

        /// <summary>
        /// The _app host
        /// </summary>
        private readonly IServerApplicationHost _appHost;
        /// <summary>
        /// The _library manager
        /// </summary>
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// The _server configuration manager
        /// </summary>
        private readonly IServerConfigurationManager _serverConfigurationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardService" /> class.
        /// </summary>
        /// <param name="taskManager">The task manager.</param>
        /// <param name="userManager">The user manager.</param>
        /// <param name="appHost">The app host.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="serverConfigurationManager">The server configuration manager.</param>
        public DashboardService(ITaskManager taskManager, IUserManager userManager, IServerApplicationHost appHost, ILibraryManager libraryManager, IServerConfigurationManager serverConfigurationManager)
        {
            _taskManager = taskManager;
            _userManager = userManager;
            _appHost = appHost;
            _libraryManager = libraryManager;
            _serverConfigurationManager = serverConfigurationManager;
        }

        /// <summary>
        /// Gets the dashboard UI path.
        /// </summary>
        /// <value>The dashboard UI path.</value>
        public string DashboardUIPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_serverConfigurationManager.Configuration.DashboardSourcePath))
                {
                    return _serverConfigurationManager.Configuration.DashboardSourcePath;
                }

                var runningDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

                return Path.Combine(runningDirectory, "dashboard-ui");
            }
        }

        /// <summary>
        /// Gets the dashboard resource path.
        /// </summary>
        /// <param name="virtualPath">The virtual path.</param>
        /// <returns>System.String.</returns>
        private string GetDashboardResourcePath(string virtualPath)
        {
            return Path.Combine(DashboardUIPath, virtualPath.Replace('/', '\\'));
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetDashboardInfo request)
        {
            var result =  GetDashboardInfo(_appHost, Logger, _taskManager, _userManager, _libraryManager).Result;

            return ResultFactory.GetOptimizedResult(RequestContext, result);
        }

        /// <summary>
        /// Gets the dashboard info.
        /// </summary>
        /// <param name="appHost">The app host.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="taskManager">The task manager.</param>
        /// <param name="userManager">The user manager.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <returns>DashboardInfo.</returns>
        public static async Task<DashboardInfo> GetDashboardInfo(IServerApplicationHost appHost, ILogger logger, ITaskManager taskManager, IUserManager userManager, ILibraryManager libraryManager)
        {
            var connections = userManager.RecentConnections.ToArray();

            var dtoBuilder = new UserDtoBuilder(logger);

            var tasks = userManager.Users.Where(u => connections.Any(c => new Guid(c.UserId) == u.Id)).Select(dtoBuilder.GetUserDto);

            var users = await Task.WhenAll(tasks).ConfigureAwait(false);

            return new DashboardInfo
            {
                SystemInfo = appHost.GetSystemInfo(),

                RunningTasks = taskManager.ScheduledTasks.Where(i => i.State == TaskState.Running || i.State == TaskState.Cancelling)
                                     .Select(ScheduledTaskHelpers.GetTaskInfo)
                                     .ToArray(),

                ApplicationUpdateTaskId = taskManager.ScheduledTasks.First(t => t.ScheduledTask.GetType().Name.Equals("SystemUpdateTask", StringComparison.OrdinalIgnoreCase)).Id,

                ActiveConnections = connections,

                Users = users.ToArray()
            };
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetDashboardConfigurationPage request)
        {
            var page = ServerEntryPoint.Instance.PluginConfigurationPages.First(p => p.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase));

            return ResultFactory.GetStaticResult(RequestContext, page.Plugin.Version.ToString().GetMD5(), page.Plugin.AssemblyDateLastModified, null, MimeTypes.GetMimeType("page.html"), () => ModifyHtml(page.GetHtmlStream()));
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetDashboardConfigurationPages request)
        {
            var pages = ServerEntryPoint.Instance.PluginConfigurationPages;

            if (request.PageType.HasValue)
            {
                pages = pages.Where(p => p.ConfigurationPageType == request.PageType.Value);
            }

            return ResultFactory.GetOptimizedResult(RequestContext, pages.Select(p => new ConfigurationPageInfo(p)).ToList());
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetDashboardResource request)
        {
            var path = request.ResourceName;

            var contentType = MimeTypes.GetMimeType(path);

            // Don't cache if not configured to do so
            // But always cache images to simulate production
            if (!_serverConfigurationManager.Configuration.EnableDashboardResponseCaching && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return ResultFactory.GetResult(GetResourceStream(path).Result, contentType);
            }

            TimeSpan? cacheDuration = null;

            // Cache images unconditionally - updates to image files will require new filename
            // If there's a version number in the query string we can cache this unconditionally
            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(request.V))
            {
                cacheDuration = TimeSpan.FromDays(365);
            }

            var assembly = GetType().Assembly.GetName();

            var cacheKey = (assembly.Version + path).GetMD5();

            return ResultFactory.GetStaticResult(RequestContext, cacheKey, null, cacheDuration, contentType, () => GetResourceStream(path));
        }

        /// <summary>
        /// Gets the resource stream.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Task{Stream}.</returns>
        private async Task<Stream> GetResourceStream(string path)
        {
            Stream resourceStream;

            if (path.Equals("scripts/all.js", StringComparison.OrdinalIgnoreCase))
            {
                resourceStream = await GetAllJavascript().ConfigureAwait(false);
            }
            else if (path.Equals("css/all.css", StringComparison.OrdinalIgnoreCase))
            {
                resourceStream = await GetAllCss().ConfigureAwait(false);
            }
            else
            {
                resourceStream = GetRawResourceStream(path);
            }

            if (resourceStream != null)
            {
                var isHtml = IsHtml(path);

                // Don't apply any caching for html pages
                // jQuery ajax doesn't seem to handle if-modified-since correctly
                if (isHtml)
                {
                    resourceStream = await ModifyHtml(resourceStream).ConfigureAwait(false);
                }
            }

            return resourceStream;
        }

        /// <summary>
        /// Gets the raw resource stream.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Task{Stream}.</returns>
        private Stream GetRawResourceStream(string path)
        {
            return new FileStream(GetDashboardResourcePath(path), FileMode.Open, FileAccess.Read, FileShare.ReadWrite, StreamDefaults.DefaultFileStreamBufferSize, true);
        }

        /// <summary>
        /// Determines whether the specified path is HTML.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns><c>true</c> if the specified path is HTML; otherwise, <c>false</c>.</returns>
        private bool IsHtml(string path)
        {
            return Path.GetExtension(path).EndsWith("html", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Modifies the HTML by adding common meta tags, css and js.
        /// </summary>
        /// <param name="sourceStream">The source stream.</param>
        /// <returns>Task{Stream}.</returns>
        internal async Task<Stream> ModifyHtml(Stream sourceStream)
        {
            string html;

            using (var memoryStream = new MemoryStream())
            {
                await sourceStream.CopyToAsync(memoryStream).ConfigureAwait(false);

                html = Encoding.UTF8.GetString(memoryStream.ToArray());
            }

            var version = GetType().Assembly.GetName().Version;

            html = html.Replace("<head>", "<head>" + GetMetaTags() + GetCommonCss(version) + GetCommonJavascript(version));

            var bytes = Encoding.UTF8.GetBytes(html);

            sourceStream.Dispose();

            return new MemoryStream(bytes);
        }

        /// <summary>
        /// Gets the meta tags.
        /// </summary>
        /// <returns>System.String.</returns>
        private static string GetMetaTags()
        {
            var sb = new StringBuilder();

            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, user-scalable=no\">");
            sb.Append("<meta name=\"apple-mobile-web-app-capable\" content=\"yes\">");
            sb.Append("<meta name=\"apple-mobile-web-app-status-bar-style\" content=\"black-translucent\">");

            // http://developer.apple.com/library/ios/#DOCUMENTATION/AppleApplications/Reference/SafariWebContent/ConfiguringWebApplications/ConfiguringWebApplications.html
            sb.Append("<link rel=\"apple-touch-icon\" href=\"css/images/touchicon.png\" />");
            sb.Append("<link rel=\"apple-touch-icon\" sizes=\"72x72\" href=\"css/images/touchicon72.png\" />");
            sb.Append("<link rel=\"apple-touch-icon\" sizes=\"114x114\" href=\"css/images/touchicon114.png\" />");
            sb.Append("<link rel=\"apple-touch-startup-image\" href=\"css/images/iossplash.png\" />");
            sb.Append("<link rel=\"shortcut icon\" href=\"favicon.ico\" />");

            return sb.ToString();
        }

        /// <summary>
        /// Gets the common CSS.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <returns>System.String.</returns>
        private static string GetCommonCss(Version version)
        {
            var versionString = "?v=" + version;

            var files = new[]
                            {
                                "http://code.jquery.com/mobile/1.3.1/jquery.mobile-1.3.1.min.css",
                                "thirdparty/video-js-3.2.0/video-js.min.css",
                                "thirdparty/video-js-3.2.0/video-js.tube.min.css",
                                "thirdparty/jqm-icon-pack-3.0/font-awesome/jqm-icon-pack-3.0.0-fa.css",
                                "css/all.css" + versionString
                            };

            var tags = files.Select(s => string.Format("<link rel=\"stylesheet\" href=\"{0}\" />", s)).ToArray();

            return string.Join(string.Empty, tags);
        }

        /// <summary>
        /// Gets the common javascript.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <returns>System.String.</returns>
        private static string GetCommonJavascript(Version version)
        {
            var versionString = "?v=" + version;

            var files = new[]
                            {
                                "http://ajax.googleapis.com/ajax/libs/jquery/1.9.1/jquery.min.js", 
                                "http://code.jquery.com/mobile/1.3.1/jquery.mobile-1.3.1.min.js",
                                "scripts/all.js" + versionString
            };

            var tags = files.Select(s => string.Format("<script src=\"{0}\"></script>", s)).ToArray();

            return string.Join(string.Empty, tags);
        }

        /// <summary>
        /// Gets a stream containing all concatenated javascript
        /// </summary>
        /// <returns>Task{Stream}.</returns>
        private async Task<Stream> GetAllJavascript()
        {
            var assembly = GetType().Assembly;

            var scriptFiles = new[]
                                  {
                                      "extensions.js",
                                      "site.js",
                                      "videojsextensions.js",
                                      "librarybrowser.js",

                                      "aboutpage.js",
                                      "addpluginpage.js",
                                      "advancedconfigurationpage.js",
                                      "advancedmetadataconfigurationpage.js",
                                      "boxsets.js",
                                      "clientsettings.js",
                                      "dashboardpage.js",
                                      "edititemmetadata.js",
                                      "edititemimages.js",
                                      "edituserpage.js",
                                      "gamesrecommendedpage.js",
                                      "gamesystemspage.js",
                                      "gamespage.js",
                                      "gamegenrepage.js",
                                      "gamestudiospage.js",
                                      "indexpage.js",
                                      "itembynamedetailpage.js",
                                      "itemdetailpage.js",
                                      "itemlistpage.js",
                                      "loginpage.js",
                                      "logpage.js",
                                      "medialibrarypage.js",
                                      "mediaplayer.js",
                                      "metadataconfigurationpage.js",
                                      "metadataimagespage.js",
                                      "moviegenres.js",
                                      "movies.js",
                                      "moviepeople.js",
                                      "moviesrecommended.js",
                                      "moviestudios.js",
                                      "movietrailers.js",
                                      "musicalbums.js",
                                      "musicartists.js",
                                      "musicgenres.js",
                                      "musicrecommended.js",
                                      "playlist.js",
                                      "plugincatalogpage.js",
                                      "pluginspage.js",
                                      "pluginupdatespage.js",
                                      "scheduledtaskpage.js",
                                      "scheduledtaskspage.js",
                                      "search.js",
                                      "songs.js",
                                      "supporterkeypage.js",
                                      "supporterpage.js",
                                      "tvgenres.js",
                                      "tvnextup.js",
                                      "tvpeople.js",
                                      "tvrecommended.js",
                                      "tvshows.js",
                                      "tvstudios.js",
                                      "updatepasswordpage.js",
                                      "userimagepage.js",
                                      "userprofilespage.js",
                                      "wizardfinishpage.js",
                                      "wizardstartpage.js",
                                      "wizarduserpage.js"
                                  };

            var memoryStream = new MemoryStream();

            var newLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

            await AppendResource(memoryStream, "thirdparty/video-js-3.2.0/video.min.js", newLineBytes).ConfigureAwait(false);
            await AppendResource(memoryStream, "thirdparty/autoNumeric.js", newLineBytes).ConfigureAwait(false);

            await AppendResource(assembly, memoryStream, "MediaBrowser.WebDashboard.ApiClient.js", newLineBytes).ConfigureAwait(false);

            foreach (var file in scriptFiles)
            {
                await AppendResource(memoryStream, "scripts/" + file, newLineBytes).ConfigureAwait(false);
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        /// <summary>
        /// Gets all CSS.
        /// </summary>
        /// <returns>Task{Stream}.</returns>
        private async Task<Stream> GetAllCss()
        {
            var files = new[]
                                  {
                                      "site.css",
                                      "librarybrowser.css",
                                      "detailtable.css",
                                      "posteritem.css",
                                      "tileitem.css",
                                      "search.css",
                                      "pluginupdates.css",
                                      "userimage.css"
                                  };

            var memoryStream = new MemoryStream();

            var newLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

            foreach (var file in files)
            {
                await AppendResource(memoryStream, "css/" + file, newLineBytes).ConfigureAwait(false);
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        /// <summary>
        /// Appends the resource.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="outputStream">The output stream.</param>
        /// <param name="path">The path.</param>
        /// <param name="newLineBytes">The new line bytes.</param>
        /// <returns>Task.</returns>
        private async Task AppendResource(Assembly assembly, Stream outputStream, string path, byte[] newLineBytes)
        {
            using (var stream = assembly.GetManifestResourceStream(path))
            {
                await stream.CopyToAsync(outputStream).ConfigureAwait(false);

                await outputStream.WriteAsync(newLineBytes, 0, newLineBytes.Length).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Appends the resource.
        /// </summary>
        /// <param name="outputStream">The output stream.</param>
        /// <param name="path">The path.</param>
        /// <param name="newLineBytes">The new line bytes.</param>
        /// <returns>Task.</returns>
        private async Task AppendResource(Stream outputStream, string path, byte[] newLineBytes)
        {
            path = GetDashboardResourcePath(path);

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, StreamDefaults.DefaultFileStreamBufferSize, true))
            {
                using (var streamReader = new StreamReader(fs))
                {
                    var text = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                    var bytes = Encoding.UTF8.GetBytes(text);
                    await outputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                }
            }

            await outputStream.WriteAsync(newLineBytes, 0, newLineBytes.Length).ConfigureAwait(false);
        }
    }

}
