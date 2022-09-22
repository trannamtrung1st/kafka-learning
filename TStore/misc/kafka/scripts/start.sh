bin/zookeeper-server-start.sh ./config-override/zookeeper.properties &
scripts/create-default-acls.sh &
sleep 7s; bin/kafka-server-start.sh ./config-override/server.properties;