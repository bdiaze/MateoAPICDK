using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Constructs;
using System;
using System.Collections.Generic;
using StageOptions = Amazon.CDK.AWS.APIGateway.StageOptions;

namespace Cdk
{
    public class CdkStack : Stack
    {
        internal CdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            string appName = System.Environment.GetEnvironmentVariable("APP_NAME")!;
            string publishZip = System.Environment.GetEnvironmentVariable("PUBLISH_ZIP")!;
            string handler = System.Environment.GetEnvironmentVariable("HANDLER")!;
            string timeout = System.Environment.GetEnvironmentVariable("TIMEOUT")!;
            string memorySize = System.Environment.GetEnvironmentVariable("MEMORY_SIZE")!;
            string domainName = System.Environment.GetEnvironmentVariable("DOMAIN_NAME")!;
            string apiMappingKey = System.Environment.GetEnvironmentVariable("API_MAPPING_KEY")!;

            // Variables de entorno de la lambda...
            string cognitoAppClientId = System.Environment.GetEnvironmentVariable("COGNITO_APP_CLIENT_ID")!;
            string cognitoUserPoolId = System.Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID")!;
            string cognitoRegion = System.Environment.GetEnvironmentVariable("COGNITO_REGION")!;
            string allowedDomains = System.Environment.GetEnvironmentVariable("ALLOWED_DOMAINS")!;

            // Creación de log group lambda...
            LogGroup logGroup = new LogGroup(this, $"{appName}APILogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de la función lambda...
            Function function = new Function(this, $"{appName}APILambdaFunction", new FunctionProps {
                Runtime = Runtime.DOTNET_8,
                Handler = handler,
                Code = Code.FromAsset(publishZip),
                FunctionName = $"{appName}APILambdaFunction",
                Timeout = Duration.Seconds(double.Parse(timeout)),
                MemorySize = double.Parse(memorySize),
                LogGroup = logGroup,
                Environment = new Dictionary<string, string> {
                    { "APP_NAME", appName },
                    { "COGNITO_APP_CLIENT_ID", cognitoAppClientId },
                    { "COGNITO_USER_POOL_ID", cognitoUserPoolId },
                    { "COGNITO_REGION", cognitoRegion },
                    { "ALLOWED_DOMAINS", allowedDomains },
                }
            });

            // Creación de access logs...
            LogGroup logGroupAccessLogs = new LogGroup(this, $"{appName}APILambdaFunctionLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/access_logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de la LambdaRestApi...
            LambdaRestApi lambdaRestApi = new LambdaRestApi(this, $"{appName}APILambdaRestApi", new LambdaRestApiProps {
                Handler = function,
                DefaultCorsPreflightOptions = new CorsOptions {
                    AllowOrigins = allowedDomains.Split(","),
                },
                DeployOptions = new StageOptions {
                    AccessLogDestination = new LogGroupLogDestination(logGroupAccessLogs),
                    AccessLogFormat = AccessLogFormat.Custom("'{\"requestTime\":\"$context.requestTime\",\"requestId\":\"$context.requestId\",\"httpMethod\":\"$context.httpMethod\",\"path\":\"$context.path\",\"resourcePath\":\"$context.resourcePath\",\"status\":$context.status,\"responseLatency\":$context.responseLatency,\"xrayTraceId\":\"$context.xrayTraceId\",\"integrationRequestId\":\"$context.integration.requestId\",\"functionResponseStatus\":\"$context.integration.status\",\"integrationLatency\":\"$context.integration.latency\",\"integrationServiceStatus\":\"$context.integration.integrationStatus\",\"authorizeStatus\":\"$context.authorize.status\",\"authorizerStatus\":\"$context.authorizer.status\",\"authorizerLatency\":\"$context.authorizer.latency\",\"authorizerRequestId\":\"$context.authorizer.requestId\",\"ip\":\"$context.identity.sourceIp\",\"userAgent\":\"$context.identity.userAgent\",\"principalId\":\"$context.authorizer.principalId\"}'"),
                    MetricsEnabled = true,
                },
                RestApiName = $"{appName}APILambdaRestApi",
            });

            // Creación de la CfnApiMapping para el API Gateway...
            CfnApiMapping apiMapping = new CfnApiMapping(this, $"{appName}APIApiMapping", new CfnApiMappingProps {
                DomainName = domainName,
                ApiMappingKey = apiMappingKey,
                ApiId = lambdaRestApi.RestApiId,
                Stage = lambdaRestApi.DeploymentStage.StageName,
            });

            // Se configura permisos para la ejecucíon de la Lambda desde el API Gateway...
            ArnPrincipal arnPrincipal = new ArnPrincipal("apigateway.amazonaws.com");
            Permission permission = new Permission {
                Scope = this,
                Action = "lambda:InvokeFunction",
                Principal = arnPrincipal,
                SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{lambdaRestApi.RestApiId}/*/*/*",
            };
            function.AddPermission($"{appName}APIPermission", permission);
        }
    }
}
