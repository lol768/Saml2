﻿using Kentor.AuthServices.Configuration;
using System;
using System.IdentityModel.Selectors;
using System.IdentityModel.Services;
using System.IdentityModel.Tokens;
using System.Security.Claims;

namespace Kentor.AuthServices
{
    /// <summary>
    /// Somewhat ugly subclassing to be able to access some methods that are protected
    /// on Saml2SecurityTokenHandler. The public interface of Saml2SecurityTokenHandler
    /// expects the actual assertion to be signed, which is not always the case when
    /// using Saml2-P. The assertion can be embedded in a signed response. Or the signing
    /// could be handled at transport level.
    /// </summary>
    class Saml2PSecurityTokenHandler : Saml2SecurityTokenHandler
    {
        public new ClaimsIdentity CreateClaims(Saml2SecurityToken samlToken)
        {
            var identity = base.CreateClaims(samlToken);

            if (Configuration.SaveBootstrapContext)
            {
                identity.BootstrapContext = new BootstrapContext(samlToken, this);
            }

            return identity;
        }

        public new void DetectReplayedToken(SecurityToken token)
        {
            base.DetectReplayedToken(token);
        }

        public new void ValidateConditions(Saml2Conditions conditions, bool enforceAudienceRestriction)
        {
            base.ValidateConditions(conditions, enforceAudienceRestriction);
        }

        private static readonly Saml2PSecurityTokenHandler defaultInstance;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static Saml2PSecurityTokenHandler()
        {
            var audienceRestriction = new AudienceRestriction(AudienceUriMode.Always);
            audienceRestriction.AllowedAudienceUris.Add(
                new Uri(KentorAuthServicesSection.Current.EntityId));

            defaultInstance = new Saml2PSecurityTokenHandler()
            {
                Configuration = new SecurityTokenHandlerConfiguration
                {
                    IssuerNameRegistry = new ReturnRequestedIssuerNameRegistry(),
                    AudienceRestriction = audienceRestriction,
                    SaveBootstrapContext = FederatedAuthentication.FederationConfiguration.IdentityConfiguration.SaveBootstrapContext
                }
            };
        }

        /// <summary>
        /// Get a default of the class, with a default implementation.
        /// </summary>
        public static Saml2PSecurityTokenHandler DefaultInstance
        {
            get
            {
                return defaultInstance;
            }
        }
    }
}