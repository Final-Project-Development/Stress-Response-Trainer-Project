from datetime import datetime, timezone
from typing import Dict

from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from pydantic import BaseModel, Field, ValidationError

app = FastAPI(title="Wearable PC Bridge")

# Holds the latest value for each metric, e.g. "heart_rate", "spo2"
latest_metrics: Dict[str, dict] = {}


class MetricEvent(BaseModel):
    device_id: str = Field(..., min_length=1)
    metric: str = Field(..., min_length=1)
    value: float
    unit: str = Field(..., min_length=1)
    timestamp: datetime
    source: str = Field(..., min_length=1)


@app.get("/")
def root() -> dict:
    return {"message": "Wearable PC Bridge is running"}


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "utc": datetime.now(timezone.utc).isoformat()}



@app.get("/latest")
def latest() -> dict:
    return latest_metrics

@app.get("/health/hr")
def latest_hr() -> dict:
    return latest_metrics.get("heart_rate", {})

@app.get("/latest/spo2")
def latest_sop2() -> dict:
    return latest_metrics.get("sop2", {})



@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket) -> None:
    await websocket.accept()
    print("Client connected")

    try:
        while True:
            raw = await websocket.receive_json()
            try:
                event = MetricEvent(**raw)
            except ValidationError as exc:
                await websocket.send_json({
                    "ok": False,
                    "error": "invalid_payload",
                    "details": exc.errors(),
                })
                continue

            latest_metrics[event.metric] = event.model_dump(mode="json")

            print(
                f"[{event.timestamp.isoformat()}] "
                f"{event.metric}={event.value} {event.unit} "
                f"(device={event.device_id}, source={event.source})"
            )

            await websocket.send_json({
                "ok": True,
                "received_metric": event.metric
            })

    except WebSocketDisconnect:
        print("Client disconnected")