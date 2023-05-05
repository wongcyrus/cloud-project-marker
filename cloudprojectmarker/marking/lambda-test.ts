import { expect } from "chai";
// if you used the '@types/mocha' method to install mocha type definitions, uncomment the following line
import "mocha";
import * as AWS from "aws-sdk";
import { Common } from "./common";

import * as chai from "chai";
import chaiSubset from "chai-subset";
import chaiString from "chai-string";
chai.use(chaiSubset);
chai.use(chaiString);

describe("Lambda", () => {
  const lambda: AWS.Lambda = new AWS.Lambda();
  const ec2: AWS.EC2 = new AWS.EC2();
  const iam: AWS.IAM = new AWS.IAM();
  const common = new Common();

  let awsAccount: string;
  before(async () => {
    awsAccount = await common.getAWSAccount();
  });

  it("should have one python3.8 Layer with MySQL Packages.", async () => {
    const layers = await lambda
      .listLayers({ CompatibleRuntime: "python3.8" })
      .promise();
    //Download your zipped package with this link
    // https://drive.google.com/file/d/1GlO9INJxPY63-wVVy-svzlgC9lKoHFGj/view?usp=sharing
    // console.log(layers);
    expect(1, "One Python Layer").to.eq(layers.Layers!.length);
  });

  it("should have one Webserver Lambda Function.", async () => {
    const layers = await lambda
      .listLayers({ CompatibleRuntime: "python3.8" })
      .promise();
    // console.log(layers);
    expect(1, "One Python Layer").to.eq(layers.Layers!.length);
    const layerArn = layers.Layers![0].LayerArn;

    // Download your lambda function code
    //https://drive.google.com/file/d/1kotPkBwQR03bzjdoOU7o3G6kxDvWYklf/view?usp=sharing
    const lambdaFunction = await lambda
      .getFunction({ FunctionName: "WebLambda" })
      .promise();

    // console.log(lambdaFunction);
    // console.log(lambdaFunction.Configuration);

    let expected = {
      FunctionName: "WebLambda",
      Runtime: "python3.8",
      Handler: "server.lambda_handler",
      Timeout: 120,
      MemorySize: 128,
      State: "Active",
    };

    expect(lambdaFunction.Configuration, "Lambda Config").to.containSubset(
      expected
    );

    // console.log(lambdaFunction!.Configuration!.Layers);
    expect(
      lambdaFunction!.Configuration!.Layers![0].Arn,
      "Lambda Layer"
    ).to.contain(layerArn);

    //Hints: you need to call secretsManager and sqs in 2 ISOLATED subnets without internet connection!
    //You need 2 more VPC Interface Endpoints which is difference from the S3 VPC Gateway Endpoint.
    //Your settings should be similar to below.
    //  {
    //       secretsManagerVpcEndpointPrimaryDNSName: 'https://vpce-0ea932be8a3d2854d-spgfqoh7.secretsmanager.us-east-1.vpce.amazonaws.com',
    //       sqsEndpointDnsEntry: 'https://vpce-0c2b0e2cc8e75457f-c73y9z4y.sqs.us-east-1.vpce.amazonaws.com',
    //       queueUrl: 'https://sqs.us-east-1.amazonaws.com/714548190053/To_Be_Processed_Queue',
    //       dbSecretArn: 'arn:aws:secretsmanager:us-east-1:714548190053:secret:AuroraServerlessMasterUserS-sODUOeZboGNc-oDLWwA'
    //       messageTableName: 'MainStack-databaseMessageTable70E9DD6C-3RIVSFWWBLBS'
    //     }
    const envVariables = lambdaFunction.Configuration!.Environment!.Variables;
    expect(envVariables, "VPC Endpoint for secretsManager").to.haveOwnProperty(
      "secretsManagerVpcEndpointPrimaryDNSName"
    );
    expect(envVariables, "VPC Endpoint for SQS").to.haveOwnProperty(
      "sqsEndpointDnsEntry"
    );
    expect(envVariables, "To_Be_Processed_Queue Url").to.haveOwnProperty(
      "queueUrl"
    );
    expect(envVariables, "secretsManager arn").to.haveOwnProperty(
      "dbSecretArn"
    );

    expect(envVariables, "DynamoDB table name").to.haveOwnProperty(
      "messageTableName"
    );
  });

  it("should have one Webserver Lambda Function in 2 Private subnets.", async () => {
    const lambdaFunction = await lambda
      .getFunction({ FunctionName: "WebLambda" })
      .promise();

    // console.log(lambdaFunction.Configuration!.VpcConfig);

    const subnets = await ec2
      .describeSubnets({
        Filters: [
          {
            Name: "subnet-id",
            Values: lambdaFunction.Configuration!.VpcConfig!.SubnetIds!,
          },
        ],
      })
      .promise();
    //console.log(subnets.Subnets);
    expect(
      subnets.Subnets![0].AvailabilityZone,
      "uses 2 subnets in different AZ"
    ).to.not.eq(subnets.Subnets![1].AvailabilityZone);

    expect(subnets.Subnets![0].CidrBlock!, "private subnet.").to.endWith("/22");
    expect(subnets.Subnets![1].CidrBlock!, "private subnet.").to.endWith("/22");

    expect(1, "1 Security Group.").to.eq(
      lambdaFunction.Configuration!.VpcConfig!.SecurityGroupIds!.length
    );

    const securityGroups = await ec2
      .describeSecurityGroups({
        GroupIds: lambdaFunction.Configuration!.VpcConfig!.SecurityGroupIds,
      })
      .promise();
    // console.log(securityGroups);
    expect("Web Lambda Security Group", "uses Web Lambda Security Group").to.eq(
      securityGroups.SecurityGroups![0].GroupName
    );
  });

  it("should use LabRole.", async () => {
    const lambdaFunction = await lambda
      .getFunction({ FunctionName: "WebLambda" })
      .promise();

    //console.log(lambdaFunction.Configuration);
    const lambdaExecutionRole = await iam
      .getRole({ RoleName: lambdaFunction.Configuration!.Role!.split("/")[1] })
      .promise();
    // console.log(
    //   decodeURIComponent(lambdaExecutionRole.Role.AssumeRolePolicyDocument!)
    // );
    let expected = "LabRole";
    const roleName = lambdaFunction.Configuration!.Role!.split("/")[1];
    expect(expected, "role can be assumed by AWS Lambda Serives").to.eq(
      roleName
    );
  });

  it("should have Resource-based policy.", async () => {
    const lambdaFunctionPolicy = await lambda
      .getPolicy({ FunctionName: "WebLambda" })
      .promise();

    // console.log(lambdaFunctionPolicy);
    const expected = {
      Effect: "Allow",
      Principal: { Service: "elasticloadbalancing.amazonaws.com" },
      Action: "lambda:InvokeFunction",
      Resource: `arn:aws:lambda:us-east-1:${awsAccount}:function:WebLambda`,
    };

    const albInvokePermission = JSON.parse(lambdaFunctionPolicy.Policy!)
      .Statement[0];
    expect(albInvokePermission, "ALB trigger web lambda.").to.containSubset(
      expected
    );
  });
});
