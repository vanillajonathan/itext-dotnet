/*
This file is part of the iText (R) project.
Copyright (c) 1998-2024 Apryse Group NV
Authors: Apryse Software.

This program is offered under a commercial and under the AGPL license.
For commercial licensing, contact us at https://itextpdf.com/sales.  For AGPL licensing, see below.

AGPL licensing:
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using System;
using iText.Bouncycastleconnector;
using iText.Commons.Bouncycastle;
using iText.Commons.Bouncycastle.Asn1.Ocsp;
using iText.Commons.Bouncycastle.Cert;
using iText.Commons.Bouncycastle.Cert.Ocsp;
using iText.Commons.Utils;
using iText.Signatures;
using iText.Signatures.Logs;
using iText.Signatures.Validation.V1.Context;
using iText.Signatures.Validation.V1.Report;

namespace iText.Signatures.Validation.V1 {
    /// <summary>Class that allows you to validate a single OCSP response.</summary>
    public class OCSPValidator {
        internal const String CERT_IS_REVOKED = "Certificate status is revoked.";

        internal const String CERT_STATUS_IS_UNKNOWN = "Certificate status is unknown.";

        internal const String INVALID_OCSP = "OCSP response is invalid.";

        internal const String ISSUERS_DO_NOT_MATCH = "OCSP: Issuers don't match.";

        internal const String FRESHNESS_CHECK = "OCSP response is not fresh enough: " + "this update: {0}, validation date: {1}, freshness: {2}.";

        internal const String OCSP_COULD_NOT_BE_VERIFIED = "OCSP response could not be verified: " + "it does not contain responder in the certificate chain and response is not signed "
             + "by issuer certificate or any from the trusted store.";

        internal const String OCSP_IS_NO_LONGER_VALID = "OCSP is no longer valid: {0} after {1}";

        internal const String SERIAL_NUMBERS_DO_NOT_MATCH = "OCSP: Serial numbers don't match.";

        internal const String UNABLE_TO_CHECK_IF_ISSUERS_MATCH = "OCSP response could not be verified: unable to check"
             + " if issuers match.";

        internal const String OCSP_CHECK = "OCSP response check.";

        private static readonly IBouncyCastleFactory BOUNCY_CASTLE_FACTORY = BouncyCastleFactoryCreator.GetFactory
            ();

        private readonly IssuingCertificateRetriever certificateRetriever;

        private readonly SignatureValidationProperties properties;

        private readonly ValidatorChainBuilder builder;

        /// <summary>
        /// Creates new
        /// <see cref="OCSPValidator"/>
        /// instance.
        /// </summary>
        /// <param name="builder">
        /// See
        /// <see cref="ValidatorChainBuilder"/>
        /// </param>
        internal OCSPValidator(ValidatorChainBuilder builder) {
            this.certificateRetriever = builder.GetCertificateRetriever();
            this.properties = builder.GetProperties();
            this.builder = builder;
        }

        /// <summary>Validates a certificate against single OCSP Response.</summary>
        /// <param name="report">to store all the chain verification results</param>
        /// <param name="context">the context in which to perform the validation</param>
        /// <param name="certificate">the certificate to check for</param>
        /// <param name="singleResp">single response to check</param>
        /// <param name="ocspResp">basic OCSP response which contains single response to check</param>
        /// <param name="validationDate">validation date to check for</param>
        public virtual void Validate(ValidationReport report, ValidationContext context, IX509Certificate certificate
            , ISingleResponse singleResp, IBasicOcspResponse ocspResp, DateTime validationDate) {
            ValidationContext localContext = context.SetValidatorContext(ValidatorContext.OCSP_VALIDATOR);
            if (CertificateUtil.IsSelfSigned(certificate)) {
                report.AddReportItem(new CertificateReportItem(certificate, OCSP_CHECK, RevocationDataValidator.SELF_SIGNED_CERTIFICATE
                    , ReportItem.ReportItemStatus.INFO));
                return;
            }
            // SingleResp contains the basic information of the status of the certificate identified by the certID.
            // Check if the serial numbers of the signCert and certID corresponds:
            if (!certificate.GetSerialNumber().Equals(singleResp.GetCertID().GetSerialNumber())) {
                report.AddReportItem(new CertificateReportItem(certificate, OCSP_CHECK, SERIAL_NUMBERS_DO_NOT_MATCH, ReportItem.ReportItemStatus
                    .INDETERMINATE));
                return;
            }
            IX509Certificate issuerCert = certificateRetriever.RetrieveIssuerCertificate(certificate);
            // Check if the issuer of the certID and signCert matches, i.e. check that issuerNameHash and issuerKeyHash
            // fields of the certID is the hash of the issuer's name and public key:
            try {
                if (!CertificateUtil.CheckIfIssuersMatch(singleResp.GetCertID(), (IX509Certificate)issuerCert)) {
                    report.AddReportItem(new CertificateReportItem(certificate, OCSP_CHECK, ISSUERS_DO_NOT_MATCH, ReportItem.ReportItemStatus
                        .INDETERMINATE));
                    return;
                }
            }
            catch (Exception) {
                report.AddReportItem(new CertificateReportItem(certificate, OCSP_CHECK, UNABLE_TO_CHECK_IF_ISSUERS_MATCH, 
                    ReportItem.ReportItemStatus.INDETERMINATE));
                return;
            }
            // So, since the issuer name and serial number identify a unique certificate, we found the single response
            // for the provided certificate.
            // Check that thisUpdate >= (validationDate - freshness).
            TimeSpan freshness = properties.GetFreshness(localContext);
            if (singleResp.GetThisUpdate().Before(DateTimeUtil.AddMillisToDate(validationDate, -(long)freshness.TotalMilliseconds
                ))) {
                report.AddReportItem(new CertificateReportItem(certificate, OCSP_CHECK, MessageFormatUtil.Format(FRESHNESS_CHECK
                    , singleResp.GetThisUpdate(), validationDate, freshness), ReportItem.ReportItemStatus.INDETERMINATE));
                return;
            }
            // If nextUpdate is not set, the responder is indicating that newer revocation information
            // is available all the time.
            if (singleResp.GetNextUpdate() != TimestampConstants.UNDEFINED_TIMESTAMP_DATE && validationDate.After(singleResp
                .GetNextUpdate())) {
                report.AddReportItem(new CertificateReportItem(certificate, OCSP_CHECK, MessageFormatUtil.Format(OCSP_IS_NO_LONGER_VALID
                    , validationDate, singleResp.GetNextUpdate()), ReportItem.ReportItemStatus.INDETERMINATE));
                return;
            }
            // Check the status of the certificate:
            ICertStatus status = singleResp.GetCertStatus();
            IRevokedCertStatus revokedStatus = BOUNCY_CASTLE_FACTORY.CreateRevokedStatus(status);
            bool isStatusGood = BOUNCY_CASTLE_FACTORY.CreateCertificateStatus().GetGood().Equals(status);
            if (isStatusGood || (revokedStatus != null && validationDate.Before(revokedStatus.GetRevocationTime()))) {
                // Check if the OCSP response is genuine.
                VerifyOcspResponder(report, localContext, ocspResp, (IX509Certificate)issuerCert);
                if (!isStatusGood) {
                    report.AddReportItem(new CertificateReportItem(certificate, OCSP_CHECK, MessageFormatUtil.Format(SignLogMessageConstant
                        .VALID_CERTIFICATE_IS_REVOKED, revokedStatus.GetRevocationTime()), ReportItem.ReportItemStatus.INFO));
                }
            }
            else {
                if (revokedStatus != null) {
                    report.AddReportItem(new CertificateReportItem(certificate, OCSP_CHECK, CERT_IS_REVOKED, ReportItem.ReportItemStatus
                        .INVALID));
                }
                else {
                    report.AddReportItem(new CertificateReportItem(certificate, OCSP_CHECK, CERT_STATUS_IS_UNKNOWN, ReportItem.ReportItemStatus
                        .INDETERMINATE));
                }
            }
        }

        /// <summary>Verifies if an OCSP response is genuine.</summary>
        /// <remarks>
        /// Verifies if an OCSP response is genuine.
        /// If it doesn't verify against the issuer certificate and response's certificates, it may verify
        /// using a trusted anchor or cert.
        /// </remarks>
        /// <param name="report">to store all the chain verification results</param>
        /// <param name="context">the context in which to perform the validation</param>
        /// <param name="ocspResp">
        /// 
        /// <see cref="iText.Commons.Bouncycastle.Asn1.Ocsp.IBasicOcspResponse"/>
        /// the OCSP response wrapper
        /// </param>
        /// <param name="issuerCert">the issuer of the certificate for which the OCSP is checked</param>
        private void VerifyOcspResponder(ValidationReport report, ValidationContext context, IBasicOcspResponse ocspResp
            , IX509Certificate issuerCert) {
            ValidationContext localContext = context.SetCertificateSource(CertificateSource.OCSP_ISSUER);
            ValidationReport responderReport = new ValidationReport();
            // OCSP response might be signed by the issuer certificate or
            // the Authorized OCSP responder certificate containing the id-kp-OCSPSigning extended key usage extension.
            IX509Certificate responderCert = null;
            // First check if the issuer certificate signed the response since it is expected to be the most common case:
            if (CertificateUtil.IsSignatureValid(ocspResp, issuerCert)) {
                responderCert = issuerCert;
            }
            // If the issuer certificate didn't sign the ocsp response, look for authorized ocsp responses
            // from the properties or from the certificate chain received with response.
            if (responderCert == null) {
                responderCert = (IX509Certificate)certificateRetriever.RetrieveOCSPResponderCertificate(ocspResp);
                if (responderCert == null) {
                    report.AddReportItem(new CertificateReportItem(issuerCert, OCSP_CHECK, OCSP_COULD_NOT_BE_VERIFIED, ReportItem.ReportItemStatus
                        .INDETERMINATE));
                    return;
                }
                if (!certificateRetriever.IsCertificateTrusted(responderCert) && !certificateRetriever.GetTrustedCertificatesStore
                    ().IsCertificateTrustedForOcsp(responderCert)) {
                    // RFC 6960 4.2.2.2. Authorized Responders:
                    // "Systems relying on OCSP responses MUST recognize a delegation certificate as being issued
                    // by the CA that issued the certificate in question only if the delegation certificate and the
                    // certificate being checked for revocation were signed by the same key."
                    // and "This certificate MUST be issued directly by the CA that is identified in the request".
                    try {
                        responderCert.Verify(issuerCert.GetPublicKey());
                    }
                    catch (Exception e) {
                        report.AddReportItem(new CertificateReportItem(responderCert, OCSP_CHECK, INVALID_OCSP, e, ReportItem.ReportItemStatus
                            .INVALID));
                        return;
                    }
                    // Validating of the ocsp signer's certificate (responderCert) described in the
                    // RFC6960 4.2.2.2.1. Revocation Checking of an Authorized Responder.
                    builder.GetCertificateChainValidator().Validate(responderReport, localContext, responderCert, ocspResp.GetProducedAt
                        ());
                    AddResponderValidationReport(report, responderReport);
                    return;
                }
            }
            builder.GetCertificateChainValidator().Validate(responderReport, localContext.SetCertificateSource(CertificateSource
                .TRUSTED), responderCert, ocspResp.GetProducedAt());
            AddResponderValidationReport(report, responderReport);
        }

        private void AddResponderValidationReport(ValidationReport report, ValidationReport responderReport) {
            foreach (ReportItem reportItem in responderReport.GetLogs()) {
                report.AddReportItem(ReportItem.ReportItemStatus.INVALID == reportItem.GetStatus() ? reportItem.SetStatus(
                    ReportItem.ReportItemStatus.INDETERMINATE) : reportItem);
            }
        }
    }
}
