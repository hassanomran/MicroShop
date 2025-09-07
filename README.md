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
