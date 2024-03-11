using Amazon.EC2;
using Amazon.EC2.Model;

public static class QueryHelper
{
    public static string GetVpcId(AmazonEC2Client acctEc2Client)
    {
        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = acctEc2Client.DescribeVpcsAsync(describeVpcsRequest).Result;
        return describeVpcsResponse.Vpcs[0].VpcId;
    }

    public static VpcEndpoint GetEndPointByServiceName(AmazonEC2Client acctEc2Client, string ServiceName)
    {
        var describeVpcEndpointsRequest = new DescribeVpcEndpointsRequest();
        describeVpcEndpointsRequest.Filters.Add(new Filter("vpc-id", [GetVpcId(acctEc2Client)]));
        describeVpcEndpointsRequest.Filters.Add(new Filter("service-name", [ServiceName]));
        var describeVpcEndpointsResponse = acctEc2Client.DescribeVpcEndpointsAsync(describeVpcEndpointsRequest).Result;
        var vpcEndpoints = describeVpcEndpointsResponse.VpcEndpoints;
        return vpcEndpoints[0];
    }

    public static SecurityGroup? GetSecurityGroupByName(AmazonEC2Client acctEc2Client, string groupName)
    {
        var vpcId = GetVpcId(acctEc2Client);
        var request = new DescribeSecurityGroupsRequest
        {
            Filters =
            [
                new("vpc-id", [vpcId]),
                new ("group-name", [groupName])
            ]
        };
        var response = acctEc2Client.DescribeSecurityGroupsAsync(request).Result;
        return response.SecurityGroups.FirstOrDefault();
    }

}