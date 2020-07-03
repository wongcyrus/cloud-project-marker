# cloudprojectmarker

## Deploy the grader lambda.

git clone https://github.com/wongcyrus/cloud-project-marker

Open samconfig.toml and change the bucket name in us-east-1.

s3_bucket = "XXXXXXX"

Run 2 commands

cd cloud-project-marker/

./deployment.sh

## Review mochawesome test report 

Use the CloudFormation Stack output TestReportBucketSecureURL.

aws cloudformation describe-stacks --stack-name cloudprojectmarker --query 'Stacks[0].Outputs[?OutputKey==`TestReportBucketSecureURL`].OutputValue' --output text

## Using Web UI for testing.

aws cloudformation describe-stacks --stack-name cloudprojectmarker --query 'Stacks[0].Outputs[?OutputKey==`CheckMarkWebUiUrl`].OutputValue' --output text

## Run Lambda Local in the current AWS Account.

sam local invoke CloudProjectMarkerFunction --env-vars env.json | jq -cr .testResult | jq . > testResult.json

## Run Lambda Local in the other AWS Account.

sam local invoke --env-vars env.json -e events/event.json CloudProjectMarkerFunction | jq -cr .testResult | jq . > testResult.json

## During test case development, Run SAM Build and Lambda Local in the other AWS Account.

sam build && sam local invoke -e events/event.json CloudProjectMarkerFunction

## Run the Lambda

CloudProjectMarkerFunction=\$(aws cloudformation describe-stacks --stack-name cloudprojectmarker \
--query 'Stacks[0].Outputs[?OutputKey==`CloudProjectMarkerFunction`].OutputValue' --output text)

echo "Check Grade"
aws lambda invoke --function-name \$CloudProjectMarkerFunction output.json

## For update

git pull

sam build
