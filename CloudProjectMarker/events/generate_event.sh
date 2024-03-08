#!/usr/bin/env python3
import re
import urllib.parse

# Read the credentials.env file
with open('credentials.env', 'r') as file:
    credentials = file.read()

# Extract the text after "aws_access_key_id="
aws_access_key_id = re.search('aws_access_key_id=(\S*)', credentials).group(1)
aws_secret_access_key = re.search('aws_secret_access_key=(\S*)', credentials).group(1)
aws_session_token = re.search('aws_session_token=(\S*)', credentials).group(1)

# Read the event.template file
with open('event.template', 'r') as file:
    event = file.read()

# Replace placeholders with URI encoded AWS credentials
event = event.replace('{aws_access_key}', urllib.parse.quote(aws_access_key_id))
event = event.replace('{aws_secret_access_key}', urllib.parse.quote(aws_secret_access_key))
event = event.replace('{aws_session_token}', urllib.parse.quote(aws_session_token))

# Write the result to event.json
with open('event.json', 'w') as file:
    file.write(event)

print('Event generated successfully')