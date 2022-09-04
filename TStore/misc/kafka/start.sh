bin/zookeeper-server-start.sh config-override/zookeeper.properties &
sleep 7s; bin/kafka-server-start.sh config-override/server.properties