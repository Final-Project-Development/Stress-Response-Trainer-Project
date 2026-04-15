import socket
import time 
import random

PORT = 5005
ADDRESS = ("127.0.0.1", PORT) 

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

while True:
    hr = random.randint(60, 100)  # Simulated heart rate value
    message = f"HR:{hr}"
    sock.sendto(message.encode("utf-8"), ADDRESS)
    print("Sent:", message)
    time.sleep(1)
