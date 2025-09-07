# 🛒 MicroShop – Microservices Demo on Kubernetes

MicroShop is a sample **microservices-based e-commerce system** built with **.NET 9, SQL Server, RabbitMQ, Ocelot API Gateway, Seq, Prometheus, and Grafana**.  
It demonstrates how to build, containerize, and deploy microservices to **Kubernetes**.

---

## 🚀 Services

- **OrderService** – Manages customer orders, stores data in SQL Server.
- **InventoryService** – Manages product stock, shares the same SQL Server database.
- **SQL Server** – Database (`OrdersDb`).
- **RabbitMQ** – Message broker for async communication.
- **API Gateway** – Uses Ocelot to route requests between services.
- **Seq** – Structured logging dashboard.
- **Prometheus & Grafana** – Monitoring and visualization.

---

## 📂 Project Structure
MicroShop/
│
├── OrderService/
├── InventoryService/
├── ApiGateway/
│ ├── Program.cs
│ ├── appsettings.json
│ ├── ocelot.json
│ └── Dockerfile
│
├── k8s/ # Kubernetes manifests
│ ├── sqlserver-pvc.yaml
│ ├── sqlserver-deployment.yaml
│ ├── orderservice-deployment.yaml
│ ├── inventoryservice-deployment.yaml
│ ├── rabbitmq-deployment.yaml
│ ├── apigateway-deployment.yaml
│ ├── seq-deployment.yaml
│ └── ingress.yaml (optional)
│
└── README.md

---

## 🛠️ Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) (with Kubernetes enabled)
- [kubectl](https://kubernetes.io/docs/tasks/tools/)
- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download)

---

## ⚙️ Setup & Deployment

### 1. Clone the repository
```sh
git clone https://github.com/<your-username>/MicroShop.git
cd MicroShop

docker build -t orderservice:latest ./OrderService
docker build -t inventoryservice:latest ./InventoryService
docker build -t apigateway:latest ./ApiGateway

kubectl apply -f k8s/sqlserver-pvc.yaml
kubectl apply -f k8s/sqlserver-deployment.yaml
kubectl apply -f k8s/rabbitmq-deployment.yaml
kubectl apply -f k8s/orderservice-deployment.yaml
kubectl apply -f k8s/inventoryservice-deployment.yaml
kubectl apply -f k8s/apigateway-deployment.yaml
kubectl apply -f k8s/seq-deployment.yaml

kubectl get pods
| Service         | URL (NodePort/Port-Forward)                                            | Notes           |
| --------------- | ---------------------------------------------------------------------- | --------------- |
| **API Gateway** | [http://localhost:30080/api/orders](http://localhost:30080/api/orders) | Main entrypoint |
| **RabbitMQ**    | [http://localhost:15672](http://localhost:15672) (port-forward)        | guest / guest   |
| **Seq**         | [http://localhost:31080](http://localhost:31080)                       | Structured logs |
| **Prometheus**  | [http://localhost:9090](http://localhost:9090) (if deployed)           | Metrics         |
| **Grafana**     | [http://localhost:3000](http://localhost:3000) (if deployed)           | Dashboards      |


POST http://localhost:30080/api/orders
Content-Type: application/json

{
  "sku": "SKU-1",
  "quantity": 2
}

GET http://localhost:30080/api/inventory
