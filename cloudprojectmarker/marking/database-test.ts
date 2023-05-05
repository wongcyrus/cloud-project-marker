import { expect } from "chai";
// if you used the '@types/mocha' method to install mocha type definitions, uncomment the following line
import "mocha";
import * as AWS from "aws-sdk";

import * as chai from "chai";
import chaiSubset from "chai-subset";
chai.use(chaiSubset);

describe("Database", () => {
  const rds: AWS.RDS = new AWS.RDS();
  const secretsManager: AWS.SecretsManager = new AWS.SecretsManager();
  const dynamoDB: AWS.DynamoDB = new AWS.DynamoDB();
  const ec2: AWS.EC2 = new AWS.EC2();

  it("should have one secret.", async () => {
    const secrets = await secretsManager.listSecrets().promise();
    // console.log(JSON.stringify(secrets));
    const dbSecret = secrets.SecretList!.find((s) =>
      s.Name!.startsWith("databaseMasterUserSecret")
    );
    // console.log(dbSecret);
    const value = await secretsManager
      .getSecretValue({ SecretId: dbSecret!.ARN! })
      .promise();
    // console.log(value);
    const expected = {
      engine: "mysql",
      port: 3306,
      username: "dbroot",
    };

    expect(
      JSON.parse(value.SecretString!),
      "secret for mysql."
    ).to.containSubset(expected);
  });
  it("should be Aurora MySQL Serverless.",  async function () {
    this.timeout(10000);
    const dBClusters = await rds
      .describeDBClusters({
        Filters: [{ Name: "db-cluster-id", Values: ["cloudprojectdatabase"] }],
      })
      .promise();
    // console.log(dBClusters.DBClusters![0]);

    const dbCluster = dBClusters.DBClusters![0];
    // console.log(JSON.stringify(dbCluster));

    const expected = {
      AllocatedStorage: 1,
      BackupRetentionPeriod: 1,
      DBClusterIdentifier: "cloudprojectdatabase",
      DBClusterParameterGroup: "default.aurora-mysql5.7",
      Status: "available",
      CustomEndpoints: [],
      MultiAZ: false,
      Engine: "aurora-mysql",
      Port: 3306,
      MasterUsername: "dbroot",
      DBClusterOptionGroupMemberships: [],
      ReadReplicaIdentifiers: [],
      DBClusterMembers: [],
      StorageEncrypted: true,
      AssociatedRoles: [],
      IAMDatabaseAuthenticationEnabled: false,
      EnabledCloudwatchLogsExports: [],
      EngineMode: "serverless",
      ScalingConfigurationInfo: {
        MinCapacity: 1,
        MaxCapacity: 16,
        AutoPause: true,
        SecondsBeforeTimeout: 300,
        SecondsUntilAutoPause: 300,
        TimeoutAction: "RollbackCapacityChange",
      },
      DeletionProtection: false,
      HttpEndpointEnabled: false,
      ActivityStreamStatus: "stopped",
      CopyTagsToSnapshot: true,
      CrossAccountClone: false,
      DomainMemberships: [],
    };

    expect(dbCluster, "Aurora Serverless Settings.").to.containSubset(expected);
    const dbSubnetGroups = await rds
      .describeDBSubnetGroups({ DBSubnetGroupName: dbCluster.DBSubnetGroup })
      .promise();
    //console.log(dbSubnetGroups.DBSubnetGroups![0]);
    const dbSubnetGroup = dbSubnetGroups.DBSubnetGroups![0];

    expect(2, "uses 2 subnets.").to.eq(dbSubnetGroup.Subnets!.length);

    const subnets = await ec2
      .describeSubnets({
        Filters: [
          {
            Name: "subnet-id",
            Values: dbSubnetGroup.Subnets!.map((c) => c.SubnetIdentifier!),
          },
        ],
      })
      .promise();
    //console.log(subnets.Subnets);
    expect(
      subnets.Subnets![0].AvailabilityZone,
      "uses 2 subnets in different AZ"
    ).to.not.eq(subnets.Subnets![1].AvailabilityZone);

    expect(subnets.Subnets![0].CidrBlock!, "private subnet.").to.contain("/22");
    expect(subnets.Subnets![1].CidrBlock!, "private subnet.").to.contain("/22");
  });

  it("should have one DynamoDB Table.", async function () {
    this.timeout(10000);
    const tables = await dynamoDB.listTables().promise();

    const messageTableName = tables!.TableNames!.find((c) =>
      c.includes("MessageTable")
    );
    // console.log(messageTableName);
    const messageTable = await dynamoDB
      .describeTable({
        TableName: messageTableName!,
      })
      .promise();
    // console.log(JSON.stringify(messageTable));

    const expected = {
      Table: {
        AttributeDefinitions: [
          {
            AttributeName: "message",
            AttributeType: "S",
          },
          {
            AttributeName: "time",
            AttributeType: "S",
          },
        ],
        TableName: messageTableName!,
        KeySchema: [
          {
            AttributeName: "message",
            KeyType: "HASH",
          },
          {
            AttributeName: "time",
            KeyType: "RANGE",
          },
        ],
        TableStatus: "ACTIVE",

        BillingModeSummary: {
          BillingMode: "PAY_PER_REQUEST",
        },
      },
    };

    expect(
      messageTable,
      "with partition key and range key in on demand mode."
    ).to.containSubset(expected);
  });
});
