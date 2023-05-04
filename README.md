# cloudprojectmarker

## Run the grader from Cloud9
Build the grader.
```
sudo yum install jq -y
git clone https://github.com/wongcyrus/cloud-project-marker
cd cloud-project-marker/
./build-layer.sh
./install_all_packages.sh
tsc
sam build
```
Run the grader
```
sam local invoke CloudProjectMarkerFunction | jq -cr .testResult | jq . > testResult.json
```


## Deploy the grader lambda.

nvm install 14
nvm alias default 14
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

## Run Lambda Local in the other AWS Account.

sam local invoke -e events/event.json CloudProjectMarkerFunction | jq -cr .testResult | jq . > testResult.json

## Run the Lambda

CloudProjectMarkerFunction=\$(aws cloudformation describe-stacks --stack-name cloudprojectmarker \
--query 'Stacks[0].Outputs[?OutputKey==`CloudProjectMarkerFunction`].OutputValue' --output text)

echo "Check Grade"
aws lambda invoke --function-name \$CloudProjectMarkerFunction output.json

## For update

git pull

sam build


## For AWS Academy Learner Lab, events/event.json in this format without session token.

{
  "graderParameter":"{\"Name\": \"Cyrus Wong\",    \"class\": \"IT114115\"}",
  "aws_access_key": "XXXXX",
  "aws_secret_access_key": "YYYY"
}


# For Educators Developing test case
Run Typescript compiler at background.

cd cloudprojectmarker
npm run watch

## During test case development, Run Quick SAM Build and Lambda Local in the other AWS Account.

./quick-test.sh

## During test case development, Run full SAM build and Lambda Local in the other AWS Account.

sam build && sam local invoke -e events/event.json CloudProjectMarkerFunction
