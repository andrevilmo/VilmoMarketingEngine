#!/usr/bin/env python3
"""Embed deploy/ into CloudFormation UserData so EC2 does not need a GitHub token."""
from __future__ import annotations

import base64
import io
import tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AWS = Path(__file__).resolve().parent
OUT = AWS / "cloudformation.yml"

FILES = [
    "docker-compose.yml",
    "gateway/nginx.conf",
    "gateway/Dockerfile",
    "stubs/echo.conf",
    "stubs/Dockerfile",
    "aws/docker-compose.aws.yml",
    "aws/install.sh",
]


def tarball() -> str:
    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w:gz") as tar:
        for rel in FILES:
            path = ROOT / rel
            tar.add(path, arcname=rel)
    return base64.b64encode(buf.getvalue()).decode("ascii")


def main() -> None:
    payload = tarball()
    # Keep lines short for YAML readability and editor limits.
    indent = "          "
    wrapped = "\n".join(
        indent + payload[i : i + 120] for i in range(0, len(payload), 120)
    )
    OUT.write_text(
        TEMPLATE.replace("__PAYLOAD__", wrapped),
        encoding="utf-8",
    )
    print(f"wrote {OUT} userdata payload {len(payload)} bytes b64")


TEMPLATE = r'''AWSTemplateFormatVersion: "2010-09-09"
Description: >
  Vilmo Marketing Engine on a single free-tier EC2 (t3.micro).
  Only port 80 is public. Postgres/Redis/app ports stay on Docker.
  Launch in sa-east-1 (Brazil) after signing in with MFA.
  Domain vilmomkt.com and Google Workspace MX are a later DNS cutover.

Parameters:
  InstanceType:
    Type: String
    Default: t3.micro
    AllowedValues: [t3.micro, t3.small]
    Description: Free-plan eligible x86 types. t3.micro is 1 GiB RAM.

  LatestAmiId:
    Type: AWS::SSM::Parameter::Value<AWS::EC2::Image::Id>
    Default: /aws/service/ami-amazon-linux-latest/al2023-ami-kernel-default-x86_64

Resources:
  VilmoRole:
    Type: AWS::IAM::Role
    Properties:
      AssumeRolePolicyDocument:
        Version: "2012-10-17"
        Statement:
          - Effect: Allow
            Principal: { Service: ec2.amazonaws.com }
            Action: sts:AssumeRole
      ManagedPolicyArns:
        - arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore
      Tags:
        - { Key: Project, Value: VilmoMarketingEngine }

  VilmoInstanceProfile:
    Type: AWS::IAM::InstanceProfile
    Properties:
      Roles: [!Ref VilmoRole]

  VilmoSecurityGroup:
    Type: AWS::EC2::SecurityGroup
    Properties:
      GroupDescription: Vilmo gateway HTTP only
      SecurityGroupIngress:
        - IpProtocol: tcp
          FromPort: 80
          ToPort: 80
          CidrIp: 0.0.0.0/0
          Description: HTTP nginx gateway
        - IpProtocol: tcp
          FromPort: 443
          ToPort: 443
          CidrIp: 0.0.0.0/0
          Description: HTTPS reserved for later ACM/nginx
      Tags:
        - { Key: Project, Value: VilmoMarketingEngine }

  VilmoInstance:
    Type: AWS::EC2::Instance
    Properties:
      ImageId: !Ref LatestAmiId
      InstanceType: !Ref InstanceType
      IamInstanceProfile: !Ref VilmoInstanceProfile
      SecurityGroupIds: [!Ref VilmoSecurityGroup]
      BlockDeviceMappings:
        - DeviceName: /dev/xvda
          Ebs:
            VolumeSize: 16
            VolumeType: gp3
            Encrypted: true
      UserData:
        Fn::Base64: |
          #!/bin/bash
          set -euxo pipefail
          mkdir -p /opt/vilmo
          cat >/tmp/vilmo-stack.b64 <<'PAYLOAD'
__PAYLOAD__
          PAYLOAD
          tr -d '[:space:]' </tmp/vilmo-stack.b64 | base64 -d | tar -xz -C /opt/vilmo
          chmod +x /opt/vilmo/aws/install.sh
          /opt/vilmo/aws/install.sh
      Tags:
        - { Key: Name, Value: vilmo-gateway }
        - { Key: Project, Value: VilmoMarketingEngine }

  VilmoEip:
    Type: AWS::EC2::EIP
    Properties:
      Domain: vpc
      Tags:
        - { Key: Name, Value: vilmo-gateway }
        - { Key: Project, Value: VilmoMarketingEngine }

  VilmoEipAssoc:
    Type: AWS::EC2::EIPAssociation
    Properties:
      AllocationId: !GetAtt VilmoEip.AllocationId
      InstanceId: !Ref VilmoInstance

  VilmoBudget:
    Type: AWS::Budgets::Budget
    Properties:
      Budget:
        BudgetName: !Sub "${AWS::StackName}-free-tier-guard"
        BudgetType: COST
        TimeUnit: MONTHLY
        BudgetLimit:
          Amount: 5
          Unit: USD
      NotificationsWithSubscribers:
        - Notification:
            NotificationType: ACTUAL
            ComparisonOperator: GREATER_THAN
            Threshold: 50
            ThresholdType: PERCENTAGE
          Subscribers:
            - SubscriptionType: EMAIL
              Address: admin@vilmomkt.com

Outputs:
  PublicIp:
    Description: Elastic IP. Point vilmomkt.com A records here later. Keep Google MX.
    Value: !Ref VilmoEip
  GatewayUrl:
    Description: Temporary URL until DNS cutover
    Value: !Sub "http://${VilmoEip}"
  HealthUrl:
    Value: !Sub "http://${VilmoEip}/health"
  SlugWeb:
    Value: !Sub "http://${VilmoEip}/web/"
  SlugApi:
    Value: !Sub "http://${VilmoEip}/api/"
  SsmHint:
    Description: Shell on the instance without opening SSH
    Value: !Sub "aws ssm start-session --target ${VilmoInstance} --region ${AWS::Region}"
'''


if __name__ == "__main__":
    main()
