﻿using Kentor.AuthServices.Saml2P;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Xml.Linq;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Kentor.AuthServices.Tests.Saml2P
{
    [TestClass]
    public class Saml2ArtifactResolveTests
    {
        [TestMethod]
        public void Saml2ArtifactResolve_ToXml()
        {
            var artifact = "MyArtifact";
            var subject = new Saml2ArtifactResolve()
            {
                Issuer = new Saml2NameIdentifier("http://sp.example.com"),
                Artifact = artifact
            };

            var actual = XElement.Parse(subject.ToXml());

            var expected = XElement.Parse(
@"<saml2p:ArtifactResolve
    xmlns:saml2p=""urn:oasis:names:tc:SAML:2.0:protocol""
    xmlns:saml2 = ""urn:oasis:names:tc:SAML:2.0:assertion""
    ID = ""_6c3a4f8b9c2d"" Version = ""2.0""
    IssueInstant = ""2004-01-21T19:00:49Z"" >
    <saml2:Issuer>http://sp.example.com</saml2:Issuer>
    <saml2p:Artifact>MyArtifact</saml2p:Artifact>
 </saml2p:ArtifactResolve>");

            // Set generated expected values to the actual.
            expected.Attribute("ID").Value = actual.Attribute("ID").Value;
            expected.Attribute("IssueInstant").Value = actual.Attribute("IssueInstant").Value;

            actual.ShouldBeEquivalentTo(expected, opt => opt.IgnoringCyclicReferences());
        }
    }
}
