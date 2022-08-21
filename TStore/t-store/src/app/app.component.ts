import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import * as base64js from 'base64-js'

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  @ViewChild('player') videoRef!: ElementRef<HTMLVideoElement>;
  @ViewChild('canvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  title = 't-store';
  videoSrc?: MediaStream;

  constructor() {
    this.navigator.getUserMedia = this.navigator.getUserMedia ||
      this.navigator.webkitGetUserMedia ||
      this.navigator.mozGetUserMedia ||
      this.navigator.msGetUserMedia;
  }

  private get navigator(): any {
    return window.navigator;
  }

  ngOnInit(): void {
    if (this.hasGetUserMedia()) {
      this.navigator.getUserMedia({ video: true, audio: true }, (localMediaStream: MediaStream) => {
        this.videoSrc = localMediaStream;
        this.videoRef.nativeElement.srcObject = localMediaStream;

        const connection = new signalR.HubConnectionBuilder()
          .withUrl("https://localhost:7227/video/stream", {
            skipNegotiation: true,
            transport: signalR.HttpTransportType.WebSockets
          })
          .build();

        connection.start().then(() => {
          const subject = new signalR.Subject();
          connection.send("SendVideoData", subject);

          setInterval(() => {
            this.canvasRef.nativeElement.getContext('2d')?.drawImage(
              this.videoRef.nativeElement, 0, 0, 320, 240);
            const image_data_url = this.canvasRef.nativeElement.toDataURL('image/jpeg');
            const base64Canvas = image_data_url.split(';base64,')[1];
            subject.next(base64Canvas);
          }, 40);
        })

      }, (e: any) => console.log(e));
    }
  }

  private hasGetUserMedia() {
    return !!(
      this.navigator.getUserMedia ||
      this.navigator.webkitGetUserMedia ||
      this.navigator.mozGetUserMedia ||
      this.navigator.msGetUserMedia
    );
  }

}
