﻿using System;
using System.Threading;
using System.Web;
using StackExchange.Profiling;
using StackExchange.Profiling.SqlFormatters;

namespace Umbraco.Core.Logging
{
    /// <summary>
    /// Implements <see cref="IProfiler"/> by using the MiniProfiler framework.
    /// </summary>
    internal class WebProfiler : IProfiler
    {
        private const string BootRequestItemKey = "Umbraco.Core.Logging.WebProfiler__isBootRequest";
        private readonly WebProfilerProvider _provider;
        private int _first;

        public WebProfiler()
        {
            // create our own provider, which can provide a profiler even during boot
            _provider = new WebProfilerProvider();

            // settings
            MiniProfiler.Settings.SqlFormatter = new SqlServerFormatter();
            MiniProfiler.Settings.StackMaxLength = 5000;
            MiniProfiler.Settings.ProfilerProvider = _provider;
        }

        public void UmbracoApplicationBeginRequest(object sender, EventArgs e)
        {
            // if this is the first request, notify our own provider that this request is the boot request
            var first = Interlocked.Exchange(ref _first, 1) == 0;
            if (first)
            {
                _provider.BeginBootRequest();
                ((HttpApplication) sender).Context.Items[BootRequestItemKey] = true;
                // and no need to start anything, profiler is already there
            }
            // else start a profiler, the normal way
            else if (ShouldProfile(sender))
                Start();
        }

        public void UmbracoApplicationEndRequest(object sender, EventArgs e)
        {
            // if this is the boot request, or if we should profile this request, stop
            // (the boot request is always profiled, no matter what)
            var isBootRequest = ((HttpApplication) sender).Context.Items[BootRequestItemKey] != null; // fixme perfs
            if (isBootRequest)
                _provider.EndBootRequest();
            if (isBootRequest || ShouldProfile(sender))
                Stop();
        }

        private static bool ShouldProfile(object sender)
        {
            var request = TryGetRequest(sender);
            if (request.Success == false) return false;

            if (request.Result.Url.IsClientSideRequest()) return false;
            if (string.IsNullOrEmpty(request.Result.QueryString["umbDebug"])) return false;
            if (request.Result.Url.IsBackOfficeRequest(HttpRuntime.AppDomainAppVirtualPath)) return false;
            return true;
        }

        /// <inheritdoc/>
        public string Render()
        {
            return MiniProfiler.RenderIncludes(RenderPosition.Right).ToString();
        }

        /// <inheritdoc/>
        public IDisposable Step(string name)
        {
            return MiniProfiler.Current.Step(name);
        }

        /// <inheritdoc/>
        public void Start()
        {
            MiniProfiler.Start();
        }

        /// <inheritdoc/>
        public void Stop(bool discardResults = false)
        {
            MiniProfiler.Stop(discardResults);
        }

        private static Attempt<HttpRequestBase> TryGetRequest(object sender)
        {
            var app = sender as HttpApplication;
            if (app == null) return Attempt<HttpRequestBase>.Fail();

            try
            {
                var req = app.Request;
                return Attempt<HttpRequestBase>.Succeed(new HttpRequestWrapper(req));
            }
            catch (HttpException ex)
            {
                return Attempt<HttpRequestBase>.Fail(ex);
            }
        }
    }
}