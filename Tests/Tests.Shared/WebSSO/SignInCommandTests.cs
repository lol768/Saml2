﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Net;
using System.Web;
using Kentor.AuthServices.Configuration;
using Kentor.AuthServices.WebSso;
using System.Collections.Generic;
using Kentor.AuthServices.TestHelpers;
using Microsoft.IdentityModel.Tokens.Saml2;
using Kentor.AuthServices.Metadata;
#if NET47
using System.IdentityModel.Metadata;
#endif

namespace Kentor.AuthServices.Tests.WebSso
{
    [TestClass]
    public class SignInCommandTests
    {
        [TestMethod]
        public void SignInCommand_Run_ReturnsAuthnRequestForDefaultIdp()
        {
            var options = StubFactory.CreateOptions();
            options.SPOptions.DiscoveryServiceUrl = null;

            var idp = options.IdentityProviders.Default;
            var defaultDestination = idp.SingleSignOnServiceUrl;

            var result = new SignInCommand().Run(
                new HttpRequestData("GET", new Uri("http://example.com")),
                options);

            result.HttpStatusCode.Should().Be(HttpStatusCode.SeeOther);
            result.Cacheability.Should().Be(Cacheability.NoCache);
            result.Location.Host.Should().Be(defaultDestination.Host);

            var queries = HttpUtility.ParseQueryString(result.Location.Query);

            queries.Should().HaveCount(2);
            queries["SAMLRequest"].Should().NotBeEmpty();
            queries["RelayState"].Should().NotBeEmpty();
        }

        [TestMethod]
        public void SignInCommand_Run_MapsReturnUrl()
        {
            var options = StubFactory.CreateOptions();
            var defaultDestination = options.IdentityProviders.Default.SingleSignOnServiceUrl;

            var httpRequest = new HttpRequestData("GET", new Uri("http://localhost/signin?ReturnUrl=%2FReturn.aspx"));

            var actual = new SignInCommand().Run(httpRequest, options);

            actual.RequestState.ReturnUrl.Should().Be("/Return.aspx");
        }

        [TestMethod]
        public void SignInCommand_Run_ChecksForLocalReturnUrl()
        {
            var options = StubFactory.CreateOptions();
            var defaultDestination = options.IdentityProviders.Default.SingleSignOnServiceUrl;
            var absoluteUri = HttpUtility.UrlEncode("http://google.com");
            var httpRequest = new HttpRequestData("GET", new Uri($"http://localhost/signin?ReturnUrl={absoluteUri}"));

            Action a = () => new SignInCommand().Run(httpRequest, options);

            a.ShouldThrow<InvalidOperationException>().WithMessage("Return Url must be a relative Url.");
        }

        [TestMethod]
        public void SignInCommand_Run_ChecksForLocalReturnUrlProtocolRelative()
        {
            var options = StubFactory.CreateOptions();
            var defaultDestination = options.IdentityProviders.Default.SingleSignOnServiceUrl;
            var absoluteUri = HttpUtility.UrlEncode("//google.com");
            var httpRequest = new HttpRequestData("GET", new Uri($"http://localhost/signin?ReturnUrl={absoluteUri}"));

            Action a = () => new SignInCommand().Run(httpRequest, options);

            a.ShouldThrow<InvalidOperationException>().WithMessage("Return Url must be a relative Url.");
        }

        [TestMethod]
        public void SignInCommand_Run_Calls_NotificationForAbsoluteUrl()
        {
            var options = StubFactory.CreateOptions();
            var defaultDestination = options.IdentityProviders.Default.SingleSignOnServiceUrl;
            var absoluteUri = HttpUtility.UrlEncode("http://google.com");
            var httpRequest = new HttpRequestData("GET", new Uri($"http://localhost/signin?ReturnUrl={absoluteUri}"));
            var validateAbsoluteReturnUrlCalled = false;

            options.Notifications.ValidateAbsoluteReturnUrl =
                (url) =>
                {
                    validateAbsoluteReturnUrlCalled = true;
                    return true;
                };
            
            Action a = () => new SignInCommand().Run(httpRequest, options);

            a.ShouldNotThrow<InvalidOperationException>("the ValidateAbsoluteReturnUrl notification returns true");
            validateAbsoluteReturnUrlCalled.Should().BeTrue("the ValidateAbsoluteReturnUrl notification should have been called");
        }

        [TestMethod]
        public void SignInCommand_Run_DoNotCalls_NotificationForRelativeUrl()
        {
            var options = StubFactory.CreateOptions();
            var defaultDestination = options.IdentityProviders.Default.SingleSignOnServiceUrl;
            var relativeUri = HttpUtility.UrlEncode("~/Secure");
            var httpRequest = new HttpRequestData("GET", new Uri($"http://localhost/signin?ReturnUrl={relativeUri}"));
            var validateAbsoluteReturnUrlCalled = false;

            options.Notifications.ValidateAbsoluteReturnUrl =
                (url) =>
                {
                    validateAbsoluteReturnUrlCalled = true;
                    return true;
                };

            Action a = () => new SignInCommand().Run(httpRequest, options);

            a.ShouldNotThrow<InvalidOperationException>("the ReturnUrl is relative");
            validateAbsoluteReturnUrlCalled.Should().BeFalse("the ValidateAbsoluteReturnUrl notification should not have been called");
        }

        [TestMethod]
        public void SignInCommand_Run_With_Idp2_ReturnsAuthnRequestForSecondIdp()
        {
            var options = StubFactory.CreateOptions();
            options.SPOptions.ServiceCertificates.Add(SignedXmlHelper.TestCert);

            var secondIdp = options.IdentityProviders[1];
            var secondDestination = secondIdp.SingleSignOnServiceUrl;
            var secondEntityId = secondIdp.EntityId;

            var request = new HttpRequestData("GET",
                new Uri("http://sp.example.com?idp=" + Uri.EscapeDataString(secondEntityId.Id)));

            var subject = new SignInCommand().Run(request, options);

            subject.Location.Host.Should().Be(secondDestination.Host);
        }

        [TestMethod]
        public void SignInCommand_Run_With_InvalidIdp_ThrowsException()
        {
            var options = StubFactory.CreateOptions();

            var request = new HttpRequestData("GET", new Uri("http://localhost/signin?idp=no-such-idp-in-config"));

            Action a = () => new SignInCommand().Run(request, options);

            a.ShouldThrow<InvalidOperationException>().WithMessage("Unknown idp no-such-idp-in-config");
        }

        [TestMethod]
        public void SignInCommand_Run_NullCheckRequest()
        {
            var options = StubFactory.CreateOptions();
            Action a = () => new SignInCommand().Run(null, options);

            a.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("request");
        }

        [TestMethod]
        public void SignInCommand_Run_NullCheckOptions()
        {
            Action a = () => new SignInCommand().Run(new HttpRequestData("GET", new Uri("http://localhost")), null);

            a.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("options");
        }

        [TestMethod]
        public void SignInCommand_Run_ReturnsRedirectToDiscoveryService()
        {
            var dsUrl = new Uri("http://ds.example.com");

            var options = new Options(new SPOptions
                {
                    DiscoveryServiceUrl = dsUrl,
                    EntityId = new Saml2NameIdentifier("https://github.com/KentorIT/authservices")
                });

            var request = new HttpRequestData("GET", new Uri("http://localhost/signin?ReturnUrl=%2FReturn%2FPath"));

            var result = new SignInCommand().Run(request, options);

            result.HttpStatusCode.Should().Be(HttpStatusCode.SeeOther);

            result.SetCookieName.Should().StartWith("Kentor.");

            var relayState = result.SetCookieName.Substring("Kentor.".Length);

            var queryString = string.Format("?entityID={0}&return={1}&returnIDParam=idp",
                Uri.EscapeDataString(options.SPOptions.EntityId.Value),
                Uri.EscapeDataString(
                    "http://localhost/AuthServices/SignIn?RelayState=" + relayState));

            var expectedLocation = new Uri(dsUrl + queryString);

            result.Location.Should().Be(expectedLocation);
            result.RequestState.ReturnUrl.Should().Be("/Return/Path");
        }

        [TestMethod]
        public void SignInCommand_Run_PublicOrigin()
        {
            var options = StubFactory.CreateOptions();
            options.SPOptions.PublicOrigin = new Uri("https://my.public.origin:8443");
              
            var idp = options.IdentityProviders.Default;

            var request = new HttpRequestData("GET",
                new Uri("http://sp.example.com?idp=" + Uri.EscapeDataString(idp.EntityId.Id)));

            var subject = new SignInCommand().Run(request, options);

            subject.Location.Host.Should().Be(new Uri("https://idp.example.com").Host);
        }

        [TestMethod]
        public void SignInCommand_Run_NullcheckOptions()
        {
            Action a = () => SignInCommand.Run(null, null, null, null, null);

            a.ShouldThrow<ArgumentNullException>()
                .And.ParamName.Should().Be("options");
        }

        [TestMethod]
        public void SignInCommand_Run_Calls_Notifications()
        {
            var options = StubFactory.CreateOptions();
            var idp = options.IdentityProviders.Default;
            var relayData = new Dictionary<string, string>();
            options.SPOptions.DiscoveryServiceUrl = null;
           
            var request = new HttpRequestData("GET",
                new Uri("http://sp.example.com"));

            var selectedIdpCalled = false;
            options.Notifications.SelectIdentityProvider =
                (ei, r) =>
            {
                ei.Should().BeSameAs(idp.EntityId);
                r.Should().BeSameAs(relayData);
                selectedIdpCalled = true;
                return null;
            };
            
            var authnRequestCreatedCalled = false;
            options.Notifications.AuthenticationRequestCreated = (a, i, r) => 
                {
                    a.Should().NotBeNull();
                    i.Should().BeSameAs(idp);
                    r.Should().BeSameAs(relayData);
                    authnRequestCreatedCalled = true;
                };

            CommandResult notifiedCommandResult = null;
            options.Notifications.SignInCommandResultCreated = (cr, r) =>
                {
                    notifiedCommandResult = cr;
                    r.Should().BeSameAs(relayData);                    
                };

            SignInCommand.Run(idp.EntityId, null, request, options, relayData)
                .Should().BeSameAs(notifiedCommandResult);

            authnRequestCreatedCalled.Should().BeTrue("the AuthenticationRequestCreated notification should have been called");
            selectedIdpCalled.Should().BeTrue("the SelectIdentityProvider notification should have been called.");
        }

        [TestMethod]
        public void SignInCommand_Run_Uses_IdpFromNotification()
        {
            var options = StubFactory.CreateOptions();
            var idp = options.IdentityProviders.Default;
            var entityId = new EntityId("urn:invalid");
            options.SPOptions.DiscoveryServiceUrl.Should().NotBeNull("this test assumes a non-null DS url");

            var request = new HttpRequestData("GET",
                new Uri("http://sp.example.com"));

            options.Notifications.SelectIdentityProvider = (ei, r) =>
            {
                return idp;
            };

            var authnRequestCreatedCalled = false;
            options.Notifications.AuthenticationRequestCreated = (a, i, r) =>
            {
                authnRequestCreatedCalled = true;
                i.Should().BeSameAs(idp, "the idp from the SelectIdentityProvider notification should override the default behaviour");
            };

            SignInCommand.Run(entityId, null, request, options, null);

            authnRequestCreatedCalled.Should().BeTrue("an AuthenticateRequest should have been created instead of going to the Discovery Service.");
        }

        [TestMethod]
        public void SignInCommand_Run_Calls_CommandResultCreated_OnRedirectToDS()
        {
            var options = StubFactory.CreateOptions();
            var idp = options.IdentityProviders.Default;
            options.SPOptions.DiscoveryServiceUrl.Should().NotBeNull("this test assumes a non-null DS url");

            var request = new HttpRequestData("GET",
                new Uri("http://sp.example.com"));

            CommandResult notifiedCommandResult = null;
            options.Notifications.SignInCommandResultCreated = (cr, r) =>
            {
                notifiedCommandResult = cr;
            };

            SignInCommand.Run(null, null, request, options, null)
                .Should().BeSameAs(notifiedCommandResult);
        }

        [TestMethod]
        public void SignInCommand_Run_CarriesOverRelayStateOnReturnFromDS()
        {
            var options = StubFactory.CreateOptions();
            var idp = options.IdentityProviders.Default;

            var relayData = new Dictionary<string, string>
            {
                { "key", "value" }
            };
            var relayState = "RelayState";
            var returnUrl = new Uri("/SignedIn", UriKind.Relative);
            var storedRequestState = new StoredRequestState(
                null,
                returnUrl,
                null,
                relayData);

            var uri = new Uri("http://sp.example.com/AuthServices/SignIn?RelayState="
                + relayState
                + "&idp="
                + Uri.EscapeDataString(idp.EntityId.Id));

            var request = new HttpRequestData("GET",
                uri,
                "/AuthServices",
                null,
                storedRequestState)
            {
                RelayState = relayState
            };

            var subject = CommandFactory.GetCommand(CommandFactory.SignInCommandName);

            var actual = subject.Run(request, options);

            actual.ClearCookieName.Should().Be("Kentor." + relayState, "cookie should be cleared");
            actual.RequestState.ReturnUrl.Should().Be(returnUrl);
            actual.RequestState.Idp.Id.Should().Be(idp.EntityId.Id);
            actual.RequestState.RelayData.ShouldBeEquivalentTo(relayData);
        }

        [TestMethod]
        public void SignInCommand_Run_WorksWithoutReturnUrlOnReturnFromDS()
        {
            var options = StubFactory.CreateOptions();
            var idp = options.IdentityProviders.Default;

            var relayData = new Dictionary<string, string>
            {
                { "key", "value" }
            };
            var relayState = "RelayState";
            var storedRequestState = new StoredRequestState(
                null,
                null,
                null,
                relayData);

            var uri = new Uri("http://sp.example.com/AuthServices/SignIn?RelayState="
                + relayState
                + "&idp="
                + Uri.EscapeDataString(idp.EntityId.Id));

            var request = new HttpRequestData("GET",
                uri,
                "/AuthServices",
                null,
                storedRequestState)
            {
                RelayState = relayState
            };

            var subject = CommandFactory.GetCommand(CommandFactory.SignInCommandName);

            var actual = subject.Run(request, options);

            actual.ClearCookieName.Should().Be("Kentor." + relayState, "cookie should be cleared");
            actual.RequestState.ReturnUrl.Should().BeNull();
            actual.RequestState.Idp.Id.Should().Be(idp.EntityId.Id);
            actual.RequestState.RelayData.ShouldBeEquivalentTo(relayData);
        }

        [TestMethod]
        public void SignInCommand_Run_ThrowsOnBothRelayStateAndReturnUrl()
        {
            var uri = new Uri("http://sp.example.com/AuthServices/SignIn?ReturnUrl=%2FLoggedIn&RelayState=state");

            var request = new HttpRequestData("GET", uri);

            var subject = CommandFactory.GetCommand(CommandFactory.SignInCommandName);

            subject.Invoking(s => s.Run(request, StubFactory.CreateOptions()))
                .ShouldThrow<InvalidOperationException>().
                WithMessage("*Both*ReturnUrl*RelayState*");
        }

        [TestMethod]
        public void SignInCommand_Run_RedirectToDsWorksWithoutSpecifiedReturnPath()
        {
            var options = StubFactory.CreateOptions();

            var request = new HttpRequestData("GET",
                new Uri("http://sp.example.com/AuthServices/SignIn"));

            Action a = () => SignInCommand.Run(null, null, request, options, null);

            a.ShouldNotThrow();
        }
    }
}