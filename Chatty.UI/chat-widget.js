class ChattyWidget extends HTMLElement {
    constructor() {
        super();
        const shadow = this.attachShadow({ mode: "open" });
        const baseApiUrl = "https://localhost:7186";
        const savedTheme = localStorage.getItem("chatty-theme") || "light";

        shadow.innerHTML = `
            <style>
                :host { all: initial; }

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

                #widget.dark {
                    background: #1f1f1f;
                    color: #e0e0e0;
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

                #header button {
                    background: none;
                    border: none;
                    color: white;
                    font-size: 18px;
                    cursor: pointer;
                    margin-left: 6px;
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

                #input-area {
                    display: flex;
                    border-top: 1px solid #ddd;
                }

                #input {
                    flex: 1;
                    padding: 10px;
                    font-size: 14px;
                    border: none;
                    outline: none;
                }

                #send-btn {
                    background: #0084FF;
                    color: white;
                    border: none;
                    padding: 0 16px;
                    cursor: pointer;
                    font-size: 16px;
                    transition: background 0.2s ease;
                }

                #send-btn:hover {
                    background: #006fd6;
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

                #widget.dark #messages {
                    background: #1f1f1f;
                }

                #widget.dark #input-area {
                    border-top: 1px solid #444;
                }

                #widget.dark #input {
                    background: #2a2a2a;
                    color: #e0e0e0;
                }

                #widget.dark .customer {
                    background: #333;
                    color: #eee;
                }

                #widget.dark .agent {
                    background: #005bb5;
                    color: white;
                }

                #widget.dark #send-btn {
                    background: #005bb5;
                }

                #widget.dark #send-btn:hover {
                    background: #0072e0;
                }
            </style>

            <div id="launcher">💬</div>
            <div id="widget" class="${savedTheme === 'dark' ? 'dark' : ''}">
                <div id="header">
                    <span>Live Chat</span>
                    <div>
                        <button id="toggle-theme" title="Toggle theme">🌙</button>
                        <button id="close-btn">×</button>
                    </div>
                </div>
                <div id="messages"></div>
                <input id="honeypot" type="text" style="display:none;" autocomplete="off" />
                <div id="input-area">
                    <input id="input" type="text" placeholder="Type a message..." />
                    <button id="send-btn">➤</button>
                </div>
                <div style="text-align: center; font-size: 11px; color: #aaa; padding: 8px;">
                    Chatty — by <span style="font-weight: bold; color: #0084FF;">JKC</span>
                </div>
            </div>
        `;

        this.initChat(shadow, baseApiUrl);
    }

    initChat(shadow, apiBase) {
        const user = "User" + Math.floor(Math.random() * 1000);
        const sessionId = this.getOrCreateSessionId();

        const launcher = shadow.getElementById("launcher");
        const widget = shadow.getElementById("widget");
        const closeBtn = shadow.getElementById("close-btn");
        const toggleThemeBtn = shadow.getElementById("toggle-theme");
        const messagesDiv = shadow.getElementById("messages");
        const input = shadow.getElementById("input");
        const sendBtn = shadow.getElementById("send-btn");

        toggleThemeBtn.onclick = () => {
            widget.classList.toggle("dark");
            const isDark = widget.classList.contains("dark");
            toggleThemeBtn.textContent = isDark ? "☀️" : "🌙";
            localStorage.setItem("chatty-theme", isDark ? "dark" : "light");
        };

        const script = document.createElement("script");
        script.src = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js";

        script.onload = async () => {

            const connection = new signalR.HubConnectionBuilder()
                .withUrl(`${apiBase}/chat-hub?role=customer&username=${user}&sessionId=${sessionId}`)
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.None) //None/Critical/Error/Warning/Information/Debug/Trace
                .build();

            await connection.on("ReceiveMessage", (fromUser, senderRole, message) => {
                if (senderRole === "customer" && message.startsWith("[System]")) return;
                this.appendMessage(messagesDiv, senderRole, message);
            });

            await connection.onclose((error) => {
                console.log("[❌ permanently disconnected]");
                showReconnectButton(); // show UI if needed
            });

            await connection.start().then(async () => {
                launcher.style.display = "flex";
                console.log("Connection start")
                await connection.invoke("JoinSession", sessionId);
            }).catch(err => {
                console.log("[❌ Failed to start connection]");
                showReconnectButton(); // fallback for negotiation failure
            });

            const sendMessage = async () => {
                const honeypot = shadow.getElementById("honeypot");
                const msg = input.value.trim();
                if (!msg || (honeypot && honeypot.value)) return;

                if (connection.state === signalR.HubConnectionState.Connected) {
                    await connection.invoke("SendMessage", sessionId, user, "customer", msg);
                }
                input.value = "";
            };

            sendBtn.onclick = sendMessage;
            input.onkeypress = e => (e.key === "Enter" ? sendMessage() : null);

            input.oninput = () => {
                const honeypot = shadow.getElementById("honeypot");
                if (honeypot && honeypot.value) return;
                if (connection.state === signalR.HubConnectionState.Connected) {
                    connection.invoke("SendTypingText", sessionId, user, input.value);
                }
            };

            launcher.onclick = async () => {
                widget.style.display = "flex";
                launcher.style.display = "none";
                console.log("launcher.onclick called");

                console.log(connection.state);
                if (connection.state === signalR.HubConnectionState.Connected)
                    await connection.invoke("JoinSession", sessionId);

                try {
                    const res = await fetch(`${apiBase}/api/chat/${sessionId}`);
                    const history = await res.json();
                    messagesDiv.innerHTML = "";
                    history.forEach(msg => {
                        if (!msg.message.includes("[System] Chat started")) {
                            this.appendMessage(messagesDiv, msg.senderRole, msg.message);
                        }
                    });
                    messagesDiv.scrollTop = messagesDiv.scrollHeight;
                } catch (err) {
                    console.log("Failed to load history:", err);
                }
                console.log("launcher.onclick _isNewSession: ", this._isNewSession);
                const res = await fetch(`${apiBase}/api/sessions/${sessionId}`);
                const sessionData = await res.json();
                const noAgent = !sessionData.assignedAgent;

                if (this._isNewSession || noAgent) {
                    await connection.invoke("SendMessage", sessionId, user, "customer", "[System] Chat started");
                    this._isNewSession = false;
                }
            };

            closeBtn.onclick = () => {
                widget.style.display = "none";
                launcher.style.display = "flex";
            };

            function showReconnectButton() {
                const newWidget = document.createElement("chatty-widget");
                document.body.appendChild(newWidget);

                // remove the old one
                const oldWidget = document.querySelector("chatty-widget");
                if (oldWidget && oldWidget !== newWidget) {
                    oldWidget.remove();
                }
            }
        };

        document.head.appendChild(script);
    }

    appendMessage(container, role, message) {
        const msg = document.createElement("div");
        msg.className = `message ${role === "agent" ? "agent" : "customer"}`;
        msg.textContent = message;

        const time = document.createElement("span");
        time.title = new Date().toLocaleString();
        time.style = "display:none; font-size:11px; color:#777; margin-top:4px;";
        msg.appendChild(time);

        msg.onmouseenter = () => (time.style.display = "block");
        msg.onmouseleave = () => (time.style.display = "none");

        container.appendChild(msg);
        container.scrollTop = container.scrollHeight;
    }

    getOrCreateSessionId() {
        // localStorage.removeItem("chat-session-id");      `
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
        document.body.appendChild(document.createElement("chatty-widget"));
    }
});

window.addEventListener("beforeunload", () => {
    localStorage.removeItem("chatty_session_id");
    localStorage.removeItem("chatty_user");
});

window.addEventListener("unhandledrejection", event => {
    const msg = event.reason?.message || "";
    if (msg.includes("Failed to fetch") || msg.includes("Failed to complete negotiation")) {
        console.warn("[⚠️ SignalR reconnect failed silently]");
        event.preventDefault();
    }
});
