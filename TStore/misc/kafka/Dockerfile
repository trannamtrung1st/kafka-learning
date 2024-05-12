FROM openjdk:8-jdk
WORKDIR /
EXPOSE 9092

RUN wget -O kafka-bin.tgz https://dlcdn.apache.org/kafka/3.7.0/kafka_2.13-3.7.0.tgz &&\
	tar -xzf kafka-bin.tgz &&\
	mv kafka_2.13-3.7.0 kafka &&\
	rm kafka-bin.tgz

WORKDIR /kafka

CMD chmod +x ./scripts/start.sh && ./scripts/start.sh

