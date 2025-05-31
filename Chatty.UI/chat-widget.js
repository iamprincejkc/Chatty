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
                    background: #0084FF;
                    color: white;
                    font-size: 24px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    cursor: pointer;
                    z-index: 10000;
                    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.25);
                }

                #widget {
                    position: fixed;
                    bottom: 80px;
                    right: 20px;
                    width: 320px;
                    height: 450px;
                    background: white;
                    border-radius: 12px;
                    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.25);
                    display: none;
                    flex-direction: column;
                    overflow: hidden;
                    font-family: 'Segoe UI', sans-serif;
                    z-index: 9999;
                }

                #header {
                    background: #0084FF;
                    color: white;
                    padding: 12px;
                    font-weight: bold;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    font-size: 16px;
                }

                #close-btn {
                    background: none;
                    border: none;
                    color: white;
                    font-size: 20px;
                    cursor: pointer;
                }

                #messages {
                    flex: 1;
                    padding: 10px;
                    overflow-y: auto;
                    font-size: 14px;
                    display: flex;
                    flex-direction: column;
                    gap: 6px;
                }

                #input {
                    border: none;
                    border-top: 1px solid #ddd;
                    padding: 10px;
                    font-size: 14px;
                    outline: none;
                }

                .message {
                    max-width: 75%;
                    padding: 8px 12px;
                    border-radius: 18px;
                    line-height: 1.4;
                    word-wrap: break-word;
                }

                .customer {
                    align-self: flex-start;
                    background: #E4E6EB;
                    color: black;
                    border-bottom-left-radius: 0;
                }

                .agent {
                    align-self: flex-end;
                    background: #0084FF;
                    color: white;
                    border-bottom-right-radius: 0;
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
                <div style="text-align: center; font-size: 11px; color: #aaa; padding: 8px;">
                Chatty — by <span style="font-weight: bold; color: #0084FF;">JKC</span>
                </div>
            </div>
        `;

        this.initChat();
    }

    initChat() {
        const user = "User" + Math.floor(Math.random() * 1000);
        const sessionId = this.getOrCreateSessionId();
        const shadow = this.shadowRoot;

        const launcher = shadow.getElementById("launcher");
        const widget = shadow.getElementById("widget");
        const closeBtn = shadow.getElementById("close-btn");
        const messagesDiv = shadow.getElementById("messages");
        const input = shadow.getElementById("input");

        const script = document.createElement("script");
        script.src = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js";

        script.onload = () => {
            const connection = new signalR.HubConnectionBuilder()
                .withUrl("https://localhost:7186/chat-hub")
                .configureLogging(signalR.LogLevel.Warning)
                .build();

            connection.on("ReceiveMessage", (fromUser, senderRole, message) => {
                if (senderRole === "customer" && message.startsWith("[System]")) return;
                this.appendMessage(messagesDiv, senderRole, message);
            });

            connection.start().then(async () => {
                launcher.style.display = "flex";
                await connection.invoke("JoinSession", sessionId);
            }).catch(err => {
                console.error("SignalR connection failed", err);
            });

            input.addEventListener("keypress", async e => {
                if (e.key === "Enter" && input.value.trim()) {
                    const honeypot = shadow.getElementById("honeypot");
                    if (honeypot && honeypot.value) return;

                    const msg = input.value.trim();
                    await connection.invoke("SendMessage", sessionId, user, "customer", msg);
                    input.value = "";
                }
            });

            input.addEventListener("input", () => {
                const honeypot = shadow.getElementById("honeypot");
                if (honeypot && honeypot.value) return; // bot detected

                const text = input.value;
                if (connection && connection.state === signalR.HubConnectionState.Connected) {
                    connection.invoke("SendTypingText", sessionId, user, text);
                }
            });

            launcher.onclick = async () => {
                widget.style.display = "flex";
                launcher.style.display = "none";
                await connection.invoke("JoinSession", sessionId);

                try {
                    const res = await fetch(`https://localhost:7186/api/chat/${sessionId}`);
                    const history = await res.json();
                    messagesDiv.innerHTML = "";

                    history.forEach(msg => {
                        if (msg.message.includes("[System] Chat started")) return;
                        this.appendMessage(messagesDiv, msg.senderRole, msg.message);
                    });

                    messagesDiv.scrollTop = messagesDiv.scrollHeight;
                } catch (err) {
                    console.error("Failed to load history:", err);
                }

                if (this._isNewSession) {

                    const label = "New User";
                    const ipAddress = await fetch("https://api.ipify.org").then(res => res.text()).catch(() => "unknown");
                    await connection.invoke("NotifyAgentNewSession", sessionId, label, ipAddress);
                    this._isNewSession = false;
                }
                await connection.invoke("SendMessage", sessionId, user, "customer", "[System] Chat started");
            };

            closeBtn.onclick = () => {
                widget.style.display = "none";
                launcher.style.display = "flex";
            };
        };

        document.head.appendChild(script);
    }

    appendMessage(container, role, message) {
        const msg = document.createElement("div");
        msg.className = `message ${role === "agent" ? "agent" : "customer"}`;
        msg.textContent = message;
        container.appendChild(msg);
        container.scrollTop = container.scrollHeight;
    }

    getOrCreateSessionId() {
        let sessionId = localStorage.getItem("chat-session-id");
        const isNew = !sessionId;

        if (isNew) {
            sessionId = crypto.randomUUID();
            localStorage.setItem("chat-session-id", sessionId);
        }

        this._isNewSession = isNew;
        return sessionId;
    }
}

customElements.define("chatty-widget", ChattyWidget);

window.addEventListener("DOMContentLoaded", () => {
    if (!document.querySelector("chatty-widget")) {
        const widget = document.createElement("chatty-widget");
        document.body.appendChild(widget);
    }
});

window.addEventListener("beforeunload", () => {
    localStorage.removeItem("chatty_session_id");
    localStorage.removeItem("chatty_user");
});
