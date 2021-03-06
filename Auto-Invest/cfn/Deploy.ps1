param (
    [String]
    $VpcId = "",
    [String]
    $AwsProfile = "busyweb-admin-role",
    [String]
    $Region = "af-south-1"
)

$IpAddress = (Invoke-WebRequest -uri "http://ifconfig.me/ip").Content
if([string]::IsNullOrEmpty($VpcId)){
    $VpcId = $(ConvertFrom-Json([string]::Join("",$(aws ec2 describe-vpcs --query "Vpcs[].VpcId" --region $Region --profile $AwsProfile))))[0]
}

$SubnetId = $(aws ec2 describe-subnets --filters Name=vpc-id,Values=$VpcId --query "Subnets[0].SubnetId" --region $Region --profile $AwsProfile)
aws cloudformation deploy `
    --template-file .\cloud-formation.yaml `
    --stack-name "auto-invest-stack" `
    --profile $AwsProfile `
    --region $Region `
    --parameter-overrides `
        UserIp="$IpAddress" `
        VPCId=$VpcId `
        KeyPair="auto-invest" `
        SubnetId=$SubnetId `
    --capabilities CAPABILITY_NAMED_IAM 