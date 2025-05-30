class ChattyWidget extends HTMLElement {
    constructor() {
        super();
        const shadow = this.attachShadow({ mode: "open" });

        shadow.innerHTML = `
            <style>
                #launcher {
                    position: fixed;
                    bottom: 20px;
                    right: 20px;
                    width: 50px;
                    height: 50px;
                    border-radius: 50%;
                    background: #007bff;
                    color: white;
                    font-size: 24px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    cursor: pointer;
                    z-index: 10000;
                    box-shadow: 0 0 10px rgba(0,0,0,0.3);
                }
                #widget {
                    position: fixed;
                    bottom: 80px;
                    right: 20px;
                    width: 300px;
                    max-height: 400px;
                    background: white;
                    border: 1px solid #ccc;
                    border-radius: 10px;
                    box-shadow: 0 0 10px rgba(0,0,0,0.2);
                    display: none;
                    flex-direction: column;
                    overflow: hidden;
                    font-family: sans-serif;
                    z-index: 9999;
                }
                #header {
                    background: #007bff;
                    color: white;
                    padding: 10px;
                    font-weight: bold;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                }
                #close-btn {
                    background: none;
                    border: none;
                    color: white;
                    font-size: 18px;
                    cursor: pointer;
                }
                #messages {
                    flex: 1;
                    padding: 10px;
                    overflow-y: auto;
                    font-size: 14px;
                }
                #input {
                    border: none;
                    border-top: 1px solid #ccc;
                    padding: 10px;
                    outline: none;
                }
            </style>

            <div id="launcher">💬</div>

            <div id="widget">
                <div id="header">
                    <span>Live Chat</span>
                    <button id="close-btn">×</button>
                </div>
                <div id="messages"></div>
                <input id="honeypot" type="text" style="display:none;" autocomplete="off" />
                <input id="input" type="text" placeholder="Type a message..." />
            </div>
        `;

        this.initChat();
    }

    initChat() {
        const user = "User" + Math.floor(Math.random() * 1000);
        const shadow = this.shadowRoot;
        const launcher = shadow.getElementById("launcher");
        const widget = shadow.getElementById("widget");
        const closeBtn = shadow.getElementById("close-btn");
        const messagesDiv = shadow.getElementById("messages");
        const input = shadow.getElementById("input");

        // 👇 Hide launcher by default until connection succeeds
        launcher.style.display = "none";

        const script = document.createElement("script");
        script.src = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js";
        script.onload = () => {
            const connection = new signalR.HubConnectionBuilder()
                .withUrl("https://localhost:7186/chat-hub")
                .configureLogging(signalR.LogLevel.Warning)
                .build();

            connection.on("ReceiveMessage", (user, message) => {
                const msg = document.createElement("div");
                msg.textContent = `${user}: ${message}`;
                messagesDiv.appendChild(msg);
            });

            connection.start().then(() => {
                // ✅ Show launcher only after successful SignalR connection
                launcher.style.display = "flex";
            }).catch(err => {
                console.error("SignalR connection failed", err);
                // ❌ Keep launcher hidden
            });

            input.addEventListener("keypress", async e => {
                if (e.key === "Enter" && input.value.trim()) {
                    const honeypot = shadow.getElementById("honeypot");
                    if (honeypot.value) {
                        console.warn("Bot detected via honeypot. Ignoring message.");
                        return;
                    }

                    const msg = input.value.trim();
                    await connection.invoke("SendMessage", user, msg);
                    input.value = "";
                }
            });
        };
        document.head.appendChild(script);

        launcher.onclick = () => widget.style.display = "flex";
        closeBtn.onclick = () => widget.style.display = "none";
    }

}

// Register the custom element
customElements.define("chatty-widget", ChattyWidget);

// 🔽 Auto-inject into DOM on load
window.addEventListener("DOMContentLoaded", () => {
    if (!document.querySelector("chatty-widget")) {
        const widget = document.createElement("chatty-widget");
        document.body.appendChild(widget);
    }
});
