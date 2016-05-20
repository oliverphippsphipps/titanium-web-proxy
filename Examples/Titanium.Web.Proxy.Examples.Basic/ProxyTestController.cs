﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace Titanium.Web.Proxy.Examples.Basic
{
    public class ProxyTestController
    {
        public void StartProxy()
        {
            ProxyServer.BeforeRequest += OnRequest;
            ProxyServer.BeforeResponse += OnResponse;
            ProxyServer.ServerCertificateValidationCallback += OnCertificateValidation;

            //Exclude Https addresses you don't want to proxy
            //Usefull for clients that use certificate pinning
            //for example dropbox.com
            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8000, true)
            {
                // ExcludedHttpsHostNameRegex = new List<string>() { "google.com", "dropbox.com" }
            };

            //An explicit endpoint is where the client knows about the existance of a proxy
            //So client sends request in a proxy friendly manner
            ProxyServer.AddEndPoint(explicitEndPoint);
            ProxyServer.Start();


            //Transparent endpoint is usefull for reverse proxying (client is not aware of the existance of proxy)
            //A transparent endpoint usually requires a network router port forwarding HTTP(S) packets to this endpoint
            //Currently do not support Server Name Indication (It is not currently supported by SslStream class)
            //That means that the transparent endpoint will always provide the same Generic Certificate to all HTTPS requests
            //In this example only google.com will work for HTTPS requests
            //Other sites will receive a certificate mismatch warning on browser
            var transparentEndPoint = new TransparentProxyEndPoint(IPAddress.Any, 8001, true)
            {
                GenericCertificateName = "google.com"
            };
            ProxyServer.AddEndPoint(transparentEndPoint);

            //ProxyServer.UpStreamHttpProxy = new ExternalProxy() { HostName = "localhost", Port = 8888 };
            //ProxyServer.UpStreamHttpsProxy = new ExternalProxy() { HostName = "localhost", Port = 8888 };

            foreach (var endPoint in ProxyServer.ProxyEndPoints)
                Console.WriteLine("Listening on '{0}' endpoint at Ip {1} and port: {2} ",
                    endPoint.GetType().Name, endPoint.IpAddress, endPoint.Port);

            //Only explicit proxies can be set as system proxy!
            ProxyServer.SetAsSystemHttpProxy(explicitEndPoint);
            ProxyServer.SetAsSystemHttpsProxy(explicitEndPoint);
        }

        public void Stop()
        {
            ProxyServer.BeforeRequest -= OnRequest;
            ProxyServer.BeforeResponse -= OnResponse;

            ProxyServer.Stop();
        }

        //intecept & cancel, redirect or update requests
        public async Task OnRequest(object sender, SessionEventArgs e)
        {
            Console.WriteLine(e.WebSession.Request.Url);

            ////read request headers
            var requestHeaders = e.WebSession.Request.RequestHeaders;

            if ((e.WebSession.Request.Method.ToUpper() == "POST" || e.WebSession.Request.Method.ToUpper() == "PUT"))
            {
                //Get/Set request body bytes
                byte[] bodyBytes = await e.GetRequestBody();
                await e.SetRequestBody(bodyBytes);

                //Get/Set request body as string
                string bodyString = await e.GetRequestBodyAsString();
                await e.SetRequestBodyString(bodyString);

            }

            //To cancel a request with a custom HTML content
            //Filter URL
            if (e.WebSession.Request.RequestUri.AbsoluteUri.Contains("google.com"))
            {
                await e.Ok("<!DOCTYPE html>" +
                      "<html><body><h1>" +
                      "Website Blocked" +
                      "</h1>" +
                      "<p>Blocked by titanium web proxy.</p>" +
                      "</body>" +
                      "</html>");
            }
            //Redirect example
            if (e.WebSession.Request.RequestUri.AbsoluteUri.Contains("wikipedia.org"))
            {
                await e.Redirect("https://www.paypal.com");
            }
        }

        //Modify response
        public async Task OnResponse(object sender, SessionEventArgs e)
        {
            //read response headers
            var responseHeaders = e.WebSession.Response.ResponseHeaders;

            //if (!e.ProxySession.Request.Host.Equals("medeczane.sgk.gov.tr")) return;
            if (e.WebSession.Request.Method == "GET" || e.WebSession.Request.Method == "POST")
            {
                if (e.WebSession.Response.ResponseStatusCode == "200")
                {
                    if (e.WebSession.Response.ContentType.Trim().ToLower().Contains("text/html"))
                    {
                        byte[] bodyBytes = await e.GetResponseBody();
                        await e.SetResponseBody(bodyBytes);

                        string body = await e.GetResponseBodyAsString();
                        await e.SetResponseBodyString(body);
                    }
                }
            }
        }

        /// <summary>
        /// Allows overriding default certificate validation logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async Task OnCertificateValidation(object sender, CertificateValidationEventArgs e)
        {
            //set IsValid to true/false based on Certificate Errors
            if (e.SslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
                e.IsValid = true;
            else
                await e.Session.Ok("Cannot validate server certificate! Not safe to proceed.");
        }
    }
}