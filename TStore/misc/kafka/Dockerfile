FROM openjdk:8-jdk
WORKDIR /
EXPOSE 9092

RUN wget -O kafka-bin.tgz https://dlcdn.apache.org/kafka/3.2.1/kafka_2.13-3.2.1.tgz &&\
	tar -xzf kafka-bin.tgz &&\
	mv kafka_2.13-3.2.1 kafka &&\
	rm kafka-bin.tgz

COPY misc/kafka/start.sh misc/kafka/stop.sh ./kafka
	
WORKDIR /kafka

CMD ./start.sh

