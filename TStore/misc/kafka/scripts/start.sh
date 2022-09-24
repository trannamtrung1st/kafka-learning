cp ./config-override/server.properties ./config/server.properties;
cp ./config-override/zookeeper.properties ./config/zookeeper.properties;

mkdir /zookeeper-data
echo "$BROKER_ID" > /zookeeper-data/myid

sed -i "s/broker.id=1/broker.id=$BROKER_ID/g" ./config/server.properties;
sed -i "s/:9093/:$LOCAL_PORT/g" ./config/server.properties;
sed -i "s/:29093/:$DOCKER_PORT/g" ./config/server.properties;

bin/zookeeper-server-start.sh ./config/zookeeper.properties &
$CREATE_ACLS = "true" && scripts/create-default-acls.sh &
sleep 7s; bin/kafka-server-start.sh ./config/server.properties;