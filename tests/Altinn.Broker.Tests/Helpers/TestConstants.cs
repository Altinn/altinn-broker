﻿namespace Altinn.Broker.Tests.Helpers;
internal class TestConstants
{
    internal const string DUMMY_SENDER_TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzY29wZSI6ImFsdGlubjpicm9rZXIud3JpdGUiLCJpc3MiOiJodHRwczovL3BsYXRmb3JtLnR0MDIuYWx0aW5uLm5vL2F1dGhlbnRpY2F0aW9uL2FwaS92MS9vcGVuaWQvIiwiYWN0dWFsX2lzcyI6ImFsdGlubi10ZXN0LXRvb2xzIiwiY2xpZW50X2FtciI6InByaXZhdGVfa2V5X2p3dCIsInRva2VuX3R5cGUiOiJCZWFyZXIiLCJleHAiOjE3MDE2OTM4OTIsImlhdCI6MTcwMTY5Mzc3MiwiY2xpZW50X2lkIjoiMDAwMDAwMDAtMDAwMC0wMDAwLTAwMDAtMDAwMDAwMDAwMDAwIiwianRpIjoiM1NyMmdWOXQ3ak04RXM5NEpTM054aFBpeTlDaEx6Z3RqV0hkRi00OWE3SSIsImNvbnN1bWVyIjp7ImF1dGhvcml0eSI6ImlzbzY1MjMtYWN0b3JpZC11cGlzIiwiSUQiOiIwMTkyOjk5MTgyNTgyNyJ9LCJ1cm46YWx0aW5uOm9yZ051bWJlciI6Ijk5MTgyNTgyNyIsInVybjphbHRpbm46YXV0aGVudGljYXRlbWV0aG9kIjoibWFza2lucG9ydGVuIiwidXJuOmFsdGlubjphdXRobGV2ZWwiOjMsIm5iZiI6MTcwNzk5NjEwOSwidXJuOmFsdGlubjpvcmciOiJ0dGQifQ.PXy87R2_ww0eH8m-2vQSOHA4J9pvcrJayq033dT1gd4";

    internal const string DUMMY_RECIPIENT_TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzY29wZSI6ImFsdGlubjpicm9rZXIucmVhZCIsImlzcyI6Imh0dHBzOi8vcGxhdGZvcm0udHQwMi5hbHRpbm4ubm8vYXV0aGVudGljYXRpb24vYXBpL3YxL29wZW5pZC8iLCJjbGllbnRfYW1yIjoicHJpdmF0ZV9rZXlfand0IiwidG9rZW5fdHlwZSI6IkJlYXJlciIsImV4cCI6MTcwMTY5Mzg5MiwiaWF0IjoxNzAxNjkzNzcyLCJjbGllbnRfaWQiOiIxMTExMTExMS0xMTExLTExMTEtMTExMS0xMTExMTExMTExMTEiLCJqdGkiOiIzU3IyZ1Y5dDdqTThFczk0SlMzTnhoUGl5OUNoTHpndGpXSGRGLTQ5YTdJIiwiY29uc3VtZXIiOnsiYXV0aG9yaXR5IjoiaXNvNjUyMy1hY3RvcmlkLXVwaXMiLCJJRCI6IjAxOTI6OTg2MjUyOTMyIn0sInVybjphbHRpbm46b3JnTnVtYmVyIjoiOTg2MjUyOTMyIiwidXJuOmFsdGlubjphdXRoZW50aWNhdGVtZXRob2QiOiJtYXNraW5wb3J0ZW4iLCJ1cm46YWx0aW5uOmF1dGhsZXZlbCI6MywiYWN0dWFsX2lzcyI6ImFsdGlubi10ZXN0LXRvb2xzIiwibmJmIjoxNzA3OTk2NTE4LCJ1cm46YWx0aW5uOm9yZyI6InR0ZCJ9.hS-oZnDXiZlwt_i0ajrmnHSHQ2usruuIZlP82ZvZjD0";

    internal const string DUMMY_LEGACY_TOKEN = "eyJraWQiOiJiZFhMRVduRGpMSGpwRThPZnl5TUp4UlJLbVo3MUxCOHUxeUREbVBpdVQwIiwiYWxnIjoiUlMyNTYifQ.eyJzY29wZSI6ImFsdGlubjpicm9rZXIubGVnYWN5IiwiaXNzIjoiaHR0cHM6Ly90ZXN0Lm1hc2tpbnBvcnRlbi5uby8iLCJjbGllbnRfYW1yIjoidmlya3NvbWhldHNzZXJ0aWZpa2F0IiwidG9rZW5fdHlwZSI6IkJlYXJlciIsImV4cCI6MTcwODU5Nzg3OCwiaWF0IjoxNzA4NTk0Mjc4LCJjbGllbnRfaWQiOiI2NzFjNTE2Zi03YWIwLTRhMTUtOTU1Yi1hODJkMTg2Y2VjYzAiLCJqdGkiOiJSTU90Mmk5NkxWZ3d4Z29NaWtXTnFNeDI0TndNZlBHc082Tl9YQjBQZzZ3IiwiY29uc3VtZXIiOnsiYXV0aG9yaXR5IjoiaXNvNjUyMy1hY3RvcmlkLXVwaXMiLCJJRCI6IjAxOTI6OTkxODI1ODI3In19.G268zp-aLUvmR1aTkkaMsZ9j6FT9FmvqKfTFOSP277F8X4BX5kLkm5v7G1MTgDybG0CUXxNGsyhMMlsGQscOZIsOe6QW05aoBFa1vWGOCsTLBaRbBm-LEU41dEPYqKzsDCh61p-zvINdNswuc5CG5vOwkKZi_PBbYUCEF6wIwe3eJ8ttNmunmEjBvOQcSIRllo-unIbzm4nsSQADnXRDAgeJ_jdl8k2s2N_Ose7qIE-usoVlKY53Ayax-V3ws8L22YxKHEbYnhx3oswfKg-ux2PrNFFFWfarUlpVnj1CFqY11ZlxXOS7sDRcwgc1gSnpTZWgysxAU0mGCoV03KwkYOMkVJp4UXkxL6WZ25RqTVb2YsIVq7g6m5BbAPJmZW-_OnpP6KZYQ8fCILYo6EIdn9TEot5Ffm8RzjlbXNseMS10oPCmQswe18TnzKaqFk2U6hOVhhakCvKxSsN0yDj9tsZitP_MOZPZ9ybVmK_jYrYNViJ02PLqnF5n3DMqcXDT";

    internal const string DUMMY_SERVICE_OWNER_TOKEN = "eyJhbGciOiJSUzI1NiIsImtpZCI6IjM4QUE3QTc5MjUzNDNCQjE0NjFCRUUwMURCNUQwOTRBM0VCOTgwMjUiLCJ0eXAiOiJKV1QiLCJ4NWMiOiIzOEFBN0E3OTI1MzQzQkIxNDYxQkVFMDFEQjVEMDk0QTNFQjk4MDI1In0.eyJzY29wZSI6ImFsdGlubjpicm9rZXIuYWRtaW4gYWx0aW5uOnJlc291cmNlcmVnaXN0cnkvcmVzb3VyY2Uud3JpdGUgYWx0aW5uOmF1dGhvcml6YXRpb246cGRwIiwidG9rZW5fdHlwZSI6IkJlYXJlciIsImV4cCI6MTcwOTEzMDc2OCwiaWF0IjoxNzA5MTI4OTY4LCJjbGllbnRfaWQiOiI3MjJhOTYyZS1hNDVlLTQ3NGMtYjgwNi05M2NhOTQ1YWYyZDIiLCJqdGkiOiJqSGF4a0VJa3FmUjRaNDFEc05TZ2FjaXJvbW9oN0J0a0lHdWlHR2RKWGNxIiwiY29uc3VtZXIiOnsiYXV0aG9yaXR5IjoiaXNvNjUyMy1hY3RvcmlkLXVwaXMiLCJJRCI6IjAxOTI6OTkxODI1ODI3In0sInVybjphbHRpbm46b3JnTnVtYmVyIjoiOTkxODI1ODI3IiwidXJuOmFsdGlubjphdXRoZW50aWNhdGVtZXRob2QiOiJtYXNraW5wb3J0ZW4iLCJ1cm46YWx0aW5uOmF1dGhsZXZlbCI6MywiaXNzIjoiaHR0cHM6Ly9wbGF0Zm9ybS50dDAyLmFsdGlubi5uby9hdXRoZW50aWNhdGlvbi9hcGkvdjEvb3BlbmlkLyIsImFjdHVhbF9pc3MiOiJhbHRpbm4tdGVzdC10b29scyIsIm5iZiI6MTcwOTEyODk2OCwidXJuOmFsdGlubjpvcmciOiJkaWdkaXIifQ.sDV1h6Vfu154z2JBEenJFtoT - 6vJol3-4GHGlGnGYr2PHZnZNNyW-t9aE0yS-KOHpUXylfSV8l8JQE_3CPwYfIfHX4Sx_C6ku29VaQ4rLVr-2UFSKdk1qrUwUsMfeS9GKYMEtvtPzF3u9kYBnqKtkO2_cNpG1jJzdrGKdgwy36APqmeUZo1c0oUcj2fShvS_vQwxizJSGwGxP4GZ8VVGGIfX2CGkVyIT4o_dECImutfMKq0RTQWxL1PKTpnaIy8gG7sbnMMqE0w2WwgJzJpqmq9wA1Ccw48MfUQRoFURjMR2lxJnVxJaoMGxoPowQQMdKlL7OSqxbh8H2e1CjJSTMw";

    internal const string RESOURCE_FOR_TEST = "altinn-broker-test-resource-1";

    internal const string RESOURCE_WITH_NO_ACCESS = "altinn-broker-test-resource-failed-access";

    internal const string RESOURCE_WITH_NO_SERVICE_OWNER = "altinn-broker-test-resource-with-blank-serviceowner";
}
