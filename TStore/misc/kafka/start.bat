start call bin/windows/zookeeper-server-start.bat config/zookeeper.properties &
timeout /t 7 & start call bin/windows/kafka-server-start.bat config/server.properties &