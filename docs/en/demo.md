# A five-minute demo

[Español](../../DEMO.md) · [Setup](getting-started.md)

Use the local browser at `http://127.0.0.1:7452`. Fictional users share `123456Aa!`.

1. **Knowledge:** choose English for a new conversation and ask “When can I swim in the pool?”. Open the citation. Explain that the answer comes from approved, versioned knowledge.
2. **Identity:** sign in as `customer-a`, then open My reservations. `customer-b` owns different inventory; chat text cannot switch identity.
3. **Action:** request “Cancel RES-001”. Explain the offer, policy and expiry. Nothing changes until you press the explicit confirmation button. Use Keep reservation if you want to preserve inventory.
4. **Reliability:** explain the recorded fault-injection test: if the source commits but its response is lost, the worker queries the original receipt. This is test evidence; the standard UI does not automatically inject a network failure.
5. **Human:** request human assistance, sign out, then sign in as `agent-assigned` and view the assigned context. `agent-unassigned` cannot read that case. The contact center is a mock.

Show the operations panel as `operations-admin` if time permits. It exposes aggregate status without conversation text. An expired session needs another login, not certificate setup.

Do not promise that RES-001 is still eligible on an old database. Dates and state evolve. Fresh presentation data can be prepared deliberately with `Preparar-Demo.cmd`, which archives the old databases.

Close with the boundary: “The reservation workflow and local inference are working. Genesys routing and the SQL Server product composition still need their own live validation.”
