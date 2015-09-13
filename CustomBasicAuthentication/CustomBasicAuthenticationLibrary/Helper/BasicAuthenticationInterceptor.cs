using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ServiceModel.Web;
using System.Web.Security;
using System.ServiceModel.Channels;
using System.Security.Principal;
using System.IdentityModel.Policy;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.IdentityModel.Claims;
using System.Net;

namespace CustomBasicAuthenticationLibrary.Helper
{
    public class BasicAuthenticationInterceptor : RequestInterceptor
    {
        MembershipProvider provider;
        string realm;

        public BasicAuthenticationInterceptor(MembershipProvider provider, string realm)
            : base(false)
        {
            this.provider = provider;
            this.realm = realm;
        }

        protected string Realm
        {
            get { return realm; }
        }

        protected MembershipProvider Provider
        {
            get { return provider; }
        }

        public override void ProcessRequest(ref RequestContext requestContext)
        {
            string[] credentials = ExtractCredentials(requestContext.RequestMessage);
            if (credentials.Length > 0 && AuthenticateUser(credentials[0], credentials[1]))
            {
                InitializeSecurityContext(requestContext.RequestMessage, credentials[0]);
            }
            else
            {
                Message reply = Message.CreateMessage(MessageVersion.None, null);
                HttpResponseMessageProperty responseProperty = new HttpResponseMessageProperty() { StatusCode = HttpStatusCode.Unauthorized };

                responseProperty.Headers.Add("WWW-Authenticate",
                    String.Format("Basic realm=\"{0}\"", Realm));

                reply.Properties[HttpResponseMessageProperty.Name] = responseProperty;
                requestContext.Reply(reply);

                requestContext = null;
            }
        }

        private bool AuthenticateUser(string username, string password)
        {
            return true;

            if (Provider.ValidateUser(username, password))
            {
                return true;
            }

            return false;
        }

        private string[] ExtractCredentials(Message requestMessage)
        {
            HttpRequestMessageProperty request = (HttpRequestMessageProperty)requestMessage.Properties[HttpRequestMessageProperty.Name];

            string authHeader = request.Headers["Authorization"];

            if (authHeader != null && authHeader.StartsWith("Basic"))
            {
                string encodedUserPass = authHeader.Substring(6).Trim();

                Encoding encoding = Encoding.GetEncoding("iso-8859-1");
                string userPass = encoding.GetString(Convert.FromBase64String(encodedUserPass));
                int separator = userPass.IndexOf(':');

                string[] credentials = new string[2];
                credentials[0] = userPass.Substring(0, separator);
                credentials[1] = userPass.Substring(separator + 1);

                return credentials;
            }

            return new string[] { };
        }

        private void InitializeSecurityContext(Message request, string username)
        {
            GenericPrincipal principal = new GenericPrincipal(new GenericIdentity(username), new string[] { });

            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>();
            policies.Add(new PrincipalAuthorizationPolicy(principal));
            ServiceSecurityContext securityContext = new ServiceSecurityContext(policies.AsReadOnly());

            if (request.Properties.Security != null)
            {
                request.Properties.Security.ServiceSecurityContext = securityContext;
            }
            else
            {
                request.Properties.Security = new SecurityMessageProperty() { ServiceSecurityContext = securityContext };
            }
        }

        class PrincipalAuthorizationPolicy : IAuthorizationPolicy
        {
            string id = Guid.NewGuid().ToString();
            IPrincipal user;

            public PrincipalAuthorizationPolicy(IPrincipal user)
            {
                this.user = user;
            }

            public ClaimSet Issuer
            {
                get { return ClaimSet.System; }
            }

            public string Id
            {
                get { return this.id; }
            }

            public bool Evaluate(EvaluationContext evaluationContext, ref object state)
            {
                evaluationContext.AddClaimSet(this, new DefaultClaimSet(Claim.CreateNameClaim(user.Identity.Name)));
                evaluationContext.Properties["Identities"] = new List<IIdentity>(new IIdentity[] { user.Identity });
                evaluationContext.Properties["Principal"] = user;
                return true;
            }
        }
    }
}
