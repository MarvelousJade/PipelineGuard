# Optional Kubernetes deployment

These manifests deploy two API, worker, and dashboard replicas with readiness/liveness probes and disruption budgets. RabbitMQ, Elasticsearch, and an OTLP collector are intentionally external dependencies; use managed or operator-backed installations for persistent production data.

Before applying:

1. Replace the placeholder image tags in `api.yaml`, `worker.yaml`, and `dashboard.yaml`.
2. Replace `RabbitMq__Uri` in `config.yaml` through a secret manager or sealed secret; do not commit a real credential.
3. Set the Elasticsearch and OTLP service addresses for the target cluster.
4. Configure an ingress and network policies appropriate to the environment.

```console
kubectl apply -k deploy/kubernetes
kubectl -n pipelineguard rollout status deployment/crash-api
kubectl -n pipelineguard rollout status deployment/crash-worker
```

The worker is safe to scale horizontally because RabbitMQ distributes deliveries and Elasticsearch document creation uses `crashId` as an immutable idempotency key.
