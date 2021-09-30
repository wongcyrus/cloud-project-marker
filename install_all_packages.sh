find . -name package.json -not -path "*/node_modules/*" -not -path "*/cdk.out/*" -not -path "*/.aws-sam/*" -exec bash -c "npm --prefix \$(dirname {}) i" \;
npm run build