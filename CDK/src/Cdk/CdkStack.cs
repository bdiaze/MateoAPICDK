using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.EC2;
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
            string vpcId = System.Environment.GetEnvironmentVariable("VPC_ID")!;
            string subnetId1 = System.Environment.GetEnvironmentVariable("SUBNET_ID_1")!;
            string subnetId2 = System.Environment.GetEnvironmentVariable("SUBNET_ID_2")!;
            string rdsSecurityGroupId = System.Environment.GetEnvironmentVariable("RDS_SECURITY_GROUP_ID")!;

            // Variables de entorno de la lambda...
            string cognitoAppClientId = System.Environment.GetEnvironmentVariable("COGNITO_APP_CLIENT_ID")!;
            string cognitoUserPoolId = System.Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID")!;
            string cognitoRegion = System.Environment.GetEnvironmentVariable("COGNITO_REGION")!;
            string allowedDomains = System.Environment.GetEnvironmentVariable("ALLOWED_DOMAINS")!;
            string connectionString = System.Environment.GetEnvironmentVariable("CONNECTION_STRING");

            // Se obtiene la VPC y subnets...
            IVpc vpc = Vpc.FromLookup(this, $"{appName}Vpc", new VpcLookupOptions {
                VpcId = vpcId
            });

            ISubnet subnet1 = Subnet.FromSubnetId(this, $"{appName}Subnet1", subnetId1);
            ISubnet subnet2 = Subnet.FromSubnetId(this, $"{appName}Subnet2", subnetId2);

            // Se crea security group para la lambda y se enlaza con security group de RDS...
            SecurityGroup securityGroup = new SecurityGroup(this, $"{appName}LambdaSecurityGroupForRDS", new SecurityGroupProps {
                Vpc = vpc,
                SecurityGroupName = $"{appName}LambdaSecurityGroupForRDS",
                Description = $"{appName} Lambda Security Group For RDS",
                AllowAllOutbound = true,
            });

            ISecurityGroup rdsSecurityGroup = SecurityGroup.FromSecurityGroupId(this, $"{appName}RDSSecurityGroup", rdsSecurityGroupId);
            rdsSecurityGroup.AddIngressRule(securityGroup, Port.POSTGRES, "Allow connection from Lambda to RDS");


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
                    { "CONNECTION_STRING", connectionString },
                },
                Vpc = vpc,
                VpcSubnets = new SubnetSelection {
                    Subnets = [subnet1, subnet2]
                },
            });

            // Creación de access logs...
            LogGroup logGroupAccessLogs = new LogGroup(this, $"{appName}APILambdaFunctionLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/access_logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            IUserPool userPool = UserPool.FromUserPoolId(this, $"{appName}APIUserPool", cognitoUserPoolId);

            // Se crea authorizer para el apigateway...
            CognitoUserPoolsAuthorizer cognitoUserPoolsAuthorizer = new CognitoUserPoolsAuthorizer(this, $"{appName}APIAuthorizer", new CognitoUserPoolsAuthorizerProps {
                CognitoUserPools = [userPool],
                AuthorizerName = $"{appName}APIAuthorizer",
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
                DefaultMethodOptions = new MethodOptions {
                    AuthorizationType = AuthorizationType.COGNITO,
                    Authorizer = cognitoUserPoolsAuthorizer
                },            
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
