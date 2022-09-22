#!/bin/bash
sleep 14s;

if [ -e /_initflag ]
then
    echo "Already ran!";
else	
	bin/kafka-acls.sh --bootstrap-server localhost:9093 --command-config ./config-override/adminclient.properties \
	  --add --transactional-id "*" \
	  --allow-principal User:transproducer \
	  --operation WRITE --operation DESCRIBE;
	touch /_initflag;
fi