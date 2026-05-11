# ASTM ↔ MQTT Bridge

A .NET 8 background service that acts as a **relay** between a laboratory instrument speaking the ASTM E1394/E1381 protocol over TCP and an MQTT broker.

## What it does

The bridge has one responsibility: **forward raw messages in both directions**.

```
Laboratory Instrument  ──(TCP / ASTM E1381 LLP)──►  Bridge  ──(MQTT)──►  Results topic
Laboratory Instrument  ◄─(TCP / ASTM E1381 LLP)──  Bridge  ◄─(MQTT)──  Orders topic
```

- **Instrument → MQTT**: Waits for the instrument to initiate an ASTM session (ENQ handshake), receives the raw ASTM message text, and publishes it as-is to the results topic.
- **MQTT → Instrument**: Subscribes to the orders topic and forwards any incoming payload as-is to the instrument over an ASTM session.
- **Status**: Publishes an online/offline JSON message to the status topic when the bridge starts or stops.

## Protocol

The instrument communication uses the **ASTM E1381 Low-Level Protocol (LLP)**:

- Each message is split into frames of at most 240 data bytes.
- Every frame is wrapped as: `STX | FN | data | ETX/ETB | checksum (2 hex digits) | CR`
- The checksum is the sum of all bytes from the frame number through ETX/ETB (inclusive), modulo 256, as two uppercase hex digits.
- Sessions are ENQ/ACK-negotiated and EOT-terminated over a persistent TCP connection.

ASTM record types supported: `H` (Header), `P` (Patient), `O` (Order), `R` (Result), `C` (Comment), `L` (Terminator).

## MQTT topics

| Direction          | Topic                                  |
|--------------------|----------------------------------------|
| Results (outbound) | `astm/results/{instrumentId}`          |
| Orders (inbound)   | `astm/orders/{instrumentId}`           |
| Status             | `astm/status/{instrumentId}`           |

Topic prefix and instrument ID are configured.

## Resilience

- Both the MQTT and instrument connections retry automatically on failure, with a 5-second delay between attempts.
- The MQTT client reconnects automatically after an unexpected disconnect.
- Only one ASTM session can run at a time (guarded by a semaphore); the TCP connection is kept alive between sessions.

