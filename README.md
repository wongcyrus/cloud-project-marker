# cloudprojectmarker


## Run the grader from Codespaces
Build the grader.
```
./install_sam_cli.sh
sudo apt install jq -y
nvm install 18
nvm alias default 18
npm install -g typescript
./build-layer.sh
./install_all_packages.sh
tsc
sam build
```

## Run the grader from Cloud9
```
./install_sam_cli.sh
sudo yum install jq -y
npm install -g typescript
./build-layer.sh
./install_all_packages.sh
cd cloudprojectmarker/
tsc
cd ..
sam build
```
Amazon Linux 2 cannot run nodejs 18 properly in May 2023, so just use default node 16.

Run the grader in Codespaces
You need to update events/event.json with your AWS Academy session key.
```
sam local invoke -e events/event.json CloudProjectMarkerFunction | jq -cr .testResult | jq . > testResult.json
```


Run the grader in AWS Cloud9.
```
sam local invoke CloudProjectMarkerFunction | jq -cr .testResult | jq . > testResult.json
```


## Deploy the grader lambda.
```
nvm install 18
nvm alias default 18
git clone https://github.com/wongcyrus/cloud-project-marker
````
Open samconfig.toml and change the bucket name in us-east-1.
```
s3_bucket = "XXXXXXX"
```
Run 2 commands
```
cd cloud-project-marker/
./deployment.sh
```

## Review mochawesome test report 

Use the CloudFormation Stack output TestReportBucketSecureURL.
```
aws cloudformation describe-stacks --stack-name cloudprojectmarker --query 'Stacks[0].Outputs[?OutputKey==`TestReportBucketSecureURL`].OutputValue' --output text
```
## Using Web UI for testing.
```
aws cloudformation describe-stacks --stack-name cloudprojectmarker --query 'Stacks[0].Outputs[?OutputKey==`CheckMarkWebUiUrl`].OutputValue' --output text
```

## Run the Lambda
```
CloudProjectMarkerFunction=\$(aws cloudformation describe-stacks --stack-name cloudprojectmarker \
--query 'Stacks[0].Outputs[?OutputKey==`CloudProjectMarkerFunction`].OutputValue' --output text)

echo "Check Grade"
aws lambda invoke --function-name \$CloudProjectMarkerFunction output.json
```

## For update
```
git pull
sam build
```

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
