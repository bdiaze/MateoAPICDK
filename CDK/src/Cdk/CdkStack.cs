using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SecretsManager;
using Amazon.CDK.AWS.SSM;
using Amazon.CDK.CustomResources;
using Constructs;
using System;
using System.Collections.Generic;
using StageOptions = Amazon.CDK.AWS.APIGateway.StageOptions;

namespace Cdk
{
    public class CdkStack : Stack
    {
        internal CdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props) {
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
            string allowedDomains = System.Environment.GetEnvironmentVariable("ALLOWED_DOMAINS")!;

            // Variables de entorno de la lambda...
            string secretArnConnectionString = System.Environment.GetEnvironmentVariable("SECRET_ARN_CONNECTION_STRING");
            string parameterArnCognitoRegion = System.Environment.GetEnvironmentVariable("PARAMETER_ARN_COGNITO_REGION")!;
            string parameterArnCognitoUserPoolId = System.Environment.GetEnvironmentVariable("PARAMETER_ARN_COGNITO_USER_POOL_ID")!;
            string parameterArnCognitoUserPoolClientId = System.Environment.GetEnvironmentVariable("PARAMETER_ARN_COGNITO_USER_POOL_CLIENT_ID")!;
            string parameterNameApiAllowedDomains = System.Environment.GetEnvironmentVariable("PARAMETER_NAME_API_ALLOWED_DOMAINS")!;

            // Se obtienen variables de entorno para la creación de la lambda de ejecución inicial...
            string appSchemaName = System.Environment.GetEnvironmentVariable("APP_SCHEMA_NAME")!;
            string privateWithInternetId1 = System.Environment.GetEnvironmentVariable("PRIVATE_WITH_INTERNET_ID_1")!;
            string privateWithInternetId2 = System.Environment.GetEnvironmentVariable("PRIVATE_WITH_INTERNET_ID_2")!;
            string initialCreationHandler = System.Environment.GetEnvironmentVariable("INITIAL_CREATION_HANDLER")!;
            string initialCreationPublishZip = System.Environment.GetEnvironmentVariable("INITIAL_CREATION_PUBLISH_ZIP")!;


            // Se obtiene la VPC y subnets...
            IVpc vpc = Vpc.FromLookup(this, $"{appName}Vpc", new VpcLookupOptions {
                VpcId = vpcId
            });

            ISubnet subnet1 = Subnet.FromSubnetId(this, $"{appName}Subnet1", subnetId1);
            ISubnet subnet2 = Subnet.FromSubnetId(this, $"{appName}Subnet2", subnetId2);

            // Se crea security group para la lambda y se enlaza con security group de RDS...
            SecurityGroup securityGroup = new(this, $"{appName}LambdaSecurityGroupForRDS", new SecurityGroupProps {
                Vpc = vpc,
                SecurityGroupName = $"{appName}LambdaSecurityGroupForRDS",
                Description = $"{appName} Lambda Security Group For RDS",
                AllowAllOutbound = true,
            });

            ISecurityGroup rdsSecurityGroup = SecurityGroup.FromSecurityGroupId(this, $"{appName}RDSSecurityGroup", rdsSecurityGroupId);
            rdsSecurityGroup.AddIngressRule(securityGroup, Port.POSTGRES, "Allow connection from Lambda to RDS");


            // Creación de log group lambda...
            LogGroup logGroup = new(this, $"{appName}APILogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            StringParameter stringParameterApiAllowedDomains = new(this, $"{appName}StringParameterAllowedDomains", new StringParameterProps {
                ParameterName = $"{parameterNameApiAllowedDomains}",
                Description = $"Allowed Domains de la aplicacion {appName}",
                StringValue = allowedDomains,
                Tier = ParameterTier.STANDARD,
            });

            // Creación de role para la función lambda...
            IRole roleLambda = new Role(this, $"{appName}APILambdaRole", new RoleProps {
                RoleName = $"{appName}APILambdaRole",
                Description = $"Role para API Lambda de {appName}",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument> {
                    {
                        $"{appName}APILambdaPolicy",
                        new PolicyDocument(new PolicyDocumentProps {
                            Statements = [
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToSecretManager",
                                    Actions = [
                                        "secretsmanager:GetSecretValue"
                                    ],
                                    Resources = [
                                        secretArnConnectionString,
                                    ],
                                }),
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToParameterStore",
                                    Actions = [
                                        "ssm:GetParameter"
                                    ],
                                    Resources = [
                                        parameterArnCognitoRegion,
                                        parameterArnCognitoUserPoolId,
                                        parameterArnCognitoUserPoolClientId,
                                        stringParameterApiAllowedDomains.ParameterArn,
                                    ],
                                })
                            ]
                        })
                    }
                }
            });

            // Creación de la función lambda...
            Function function = new(this, $"{appName}APILambdaFunction", new FunctionProps {
                Runtime = Runtime.DOTNET_8,
                Handler = handler,
                Code = Code.FromAsset(publishZip),
                FunctionName = $"{appName}APILambdaFunction",
                Timeout = Duration.Seconds(double.Parse(timeout)),
                MemorySize = double.Parse(memorySize),
                Architecture = Architecture.ARM_64,
                LogGroup = logGroup,
                Environment = new Dictionary<string, string> {
                    { "APP_NAME", appName },
                    { "SECRET_ARN_CONNECTION_STRING", secretArnConnectionString },
                    { "PARAMETER_ARN_COGNITO_REGION", parameterArnCognitoRegion },
                    { "PARAMETER_ARN_COGNITO_USER_POOL_ID", parameterArnCognitoUserPoolId },
                    { "PARAMETER_ARN_COGNITO_USER_POOL_CLIENT_ID", parameterArnCognitoUserPoolClientId },
                    { "PARAMETER_ARN_API_ALLOWED_DOMAINS", stringParameterApiAllowedDomains.ParameterArn },
                },
                Vpc = vpc,
                VpcSubnets = new SubnetSelection {
                    Subnets = [subnet1, subnet2]
                },
                SecurityGroups = [securityGroup],
                Role = roleLambda,
            });

            // Creación de access logs...
            LogGroup logGroupAccessLogs = new(this, $"{appName}APILambdaFunctionLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/access_logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Se crea authorizer para el apigateway...
            IStringParameter stringParameterCognitoUserPoolId = StringParameter.FromStringParameterArn(this, $"{appName}StringParameterCognitoUserPoolId", parameterArnCognitoUserPoolId);
            IUserPool userPool = UserPool.FromUserPoolId(this, $"{appName}APIUserPool", stringParameterCognitoUserPoolId.StringValue);
            CognitoUserPoolsAuthorizer cognitoUserPoolsAuthorizer = new(this, $"{appName}APIAuthorizer", new CognitoUserPoolsAuthorizerProps {
                CognitoUserPools = [userPool],
                AuthorizerName = $"{appName}APIAuthorizer",
            });

            // Creación de la LambdaRestApi...
            LambdaRestApi lambdaRestApi = new(this, $"{appName}APILambdaRestApi", new LambdaRestApiProps {
                Handler = function,
                DefaultCorsPreflightOptions = new CorsOptions {
                    AllowOrigins = stringParameterApiAllowedDomains.StringValue.Split(","),
                },
                DeployOptions = new StageOptions {
                    AccessLogDestination = new LogGroupLogDestination(logGroupAccessLogs),
                    AccessLogFormat = AccessLogFormat.Custom("'{\"requestTime\":\"$context.requestTime\",\"requestId\":\"$context.requestId\",\"httpMethod\":\"$context.httpMethod\",\"path\":\"$context.path\",\"resourcePath\":\"$context.resourcePath\",\"status\":$context.status,\"responseLatency\":$context.responseLatency,\"xrayTraceId\":\"$context.xrayTraceId\",\"integrationRequestId\":\"$context.integration.requestId\",\"functionResponseStatus\":\"$context.integration.status\",\"integrationLatency\":\"$context.integration.latency\",\"integrationServiceStatus\":\"$context.integration.integrationStatus\",\"authorizeStatus\":\"$context.authorize.status\",\"authorizerStatus\":\"$context.authorizer.status\",\"authorizerLatency\":\"$context.authorizer.latency\",\"authorizerRequestId\":\"$context.authorizer.requestId\",\"ip\":\"$context.identity.sourceIp\",\"userAgent\":\"$context.identity.userAgent\",\"principalId\":\"$context.authorizer.principalId\"}'"),
                    MetricsEnabled = true,
                    StageName = "prod",
                    Description = $"Stage para produccion de la aplicacion {appName}",
                },
                RestApiName = $"{appName}APILambdaRestApi",
                DefaultMethodOptions = new MethodOptions {
                    AuthorizationType = AuthorizationType.COGNITO,
                    Authorizer = cognitoUserPoolsAuthorizer
                },     
            });

            // Creación de la CfnApiMapping para el API Gateway...
            CfnApiMapping apiMapping = new(this, $"{appName}APIApiMapping", new CfnApiMappingProps {
                DomainName = domainName,
                ApiMappingKey = apiMappingKey,
                ApiId = lambdaRestApi.RestApiId,
                Stage = lambdaRestApi.DeploymentStage.StageName,
            });

            // Se configura permisos para la ejecucíon de la Lambda desde el API Gateway...
            ArnPrincipal arnPrincipal = new("apigateway.amazonaws.com");
            Permission permission = new() {
                Scope = this,
                Action = "lambda:InvokeFunction",
                Principal = arnPrincipal,
                SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{lambdaRestApi.RestApiId}/*/*/*",
            };
            function.AddPermission($"{appName}APIPermission", permission);

            #region Initial Creation Lambda
            // Se crea función lambda que ejecute scripts para la creación del esquema, usuario de aplicación y migración de EFCore...
            // Primero creación de log group lambda de creación inicial...
            LogGroup logGroupInitialLambda = new(this, $"{appName}APIInitialCreationLambdaLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APIInitialCreationLambda/logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Luego la creación del rol para la función lambda...
            IRole roleInitialLambda = new Role(this, $"{appName}APIInitialCreationLambdaRole", new RoleProps {
                RoleName = $"{appName}APIInitialCreationLambdaRole",
                Description = $"Role para Lambda de creacion inicial {appName}",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument> {
                    {
                        $"{appName}APIInitialCreationLambdaPolicy",
                        new PolicyDocument(new PolicyDocumentProps {
                            Statements = [
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToSecretManager",
                                    Actions = [
                                        "secretsmanager:GetSecretValue"
                                    ],
                                    Resources = [
                                        secretArnConnectionString,
                                    ],
                                })
                            ]
                        })
                    }
                }
            });

            // Y el security group...
            SecurityGroup securityGroupInitialLambda = new(this, $"{appName}APIInitialCreationLambdaSecurityGroup", new SecurityGroupProps {
                Vpc = vpc,
                SecurityGroupName = $"{appName}APIInitialCreationLambdaSecurityGroup",
                Description = $"Security Group para Lambda de creacion inicial {appName}",
                AllowAllOutbound = true
            });
            rdsSecurityGroup.AddIngressRule(Peer.SecurityGroupId(securityGroupInitialLambda.SecurityGroupId), Port.POSTGRES, $"Ingress para funcion lambda de creacion inicial {appName}");

            // Creación de la función lambda
            ISubnet privateWithInternet1 = Subnet.FromSubnetId(this, $"{appName}PrivateSubnetWithInternet1", privateWithInternetId1);
            ISubnet privateWithInternet2 = Subnet.FromSubnetId(this, $"{appName}PrivateSubnetWithInternet2", privateWithInternetId2);
            Function functionInitial = new(this, $"{appName}APIInitialCreationLambda", new FunctionProps {
                Runtime = Runtime.DOTNET_8,
                Handler = initialCreationHandler,
                Code = Code.FromAsset(initialCreationPublishZip),
                FunctionName = $"{appName}APIInitialCreationLambda",
                Timeout = Duration.Seconds(2 * 60),
                MemorySize = 256,
                Architecture = Architecture.ARM_64,
                LogGroup = logGroupInitialLambda,
                Environment = new Dictionary<string, string> {
                    { "SECRET_ARN_CONNECTION_STRING", secretArnConnectionString },
                    { "APP_NAME", appName },
                    { "APP_SCHEMA_NAME", appSchemaName },
                },
                Vpc = vpc,
                VpcSubnets = new SubnetSelection {
                    Subnets = [privateWithInternet1, privateWithInternet2]
                },
                SecurityGroups = [securityGroupInitialLambda],
                Role = roleInitialLambda,
            });

            // Se gatilla la lambda...
            _ = new AwsCustomResource(this, $"{appName}APIInitialCreationTrigger", new AwsCustomResourceProps {
                Policy = AwsCustomResourcePolicy.FromStatements([
                    new PolicyStatement(new PolicyStatementProps{
                        Actions = [ "lambda:InvokeFunction" ],
                        Resources = [functionInitial.FunctionArn ]
                    })
                ]),
                Timeout = Duration.Seconds(2 * 60),
                OnUpdate = new AwsSdkCall {
                    Service = "Lambda",
                    Action = "invoke",
                    Parameters = new Dictionary<string, object> {
                        { "FunctionName", functionInitial.FunctionName },
                        { "InvocationType", "Event" },
                        { "Payload", "\"\"" }
                    },
                    PhysicalResourceId = PhysicalResourceId.Of(DateTime.Now.ToString())
                }
            });
            #endregion
        }
    }
}
