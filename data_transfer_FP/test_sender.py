import asyncio
import json
from datetime import datetime, timezone

import websockets


async def main() -> None:
    uri = "ws://127.0.0.1:8765/ws"

    async with websockets.connect(uri) as ws:
        samples = [
            {
                "device_id": "huawei_gt2_pro",
                "metric": "heart_rate",
                "value": 78,
                "unit": "bpm",
                "timestamp": datetime.now(timezone.utc).isoformat(),
                "source": "test_sender",
            },
            {
                "device_id": "huawei_gt2_pro",
                "metric": "spo2",
                "value": 97,
                "unit": "percent",
                "timestamp": datetime.now(timezone.utc).isoformat(),
                "source": "test_sender",
            },
        ]

        for payload in samples:
            await ws.send(json.dumps(payload))
            reply = await ws.recv()
            print("Server reply:", reply)


if __name__ == "__main__":
    asyncio.run(main())