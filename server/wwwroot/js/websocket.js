const socket = new WebSocket("ws://localhost:5088/ws");

socket.onopen = function (event) {
  console.log("WebSocket is open now.");
  sendMessage({ action: "test", message: "Hello Server!" });
};

socket.onmessage = function (event) {
  console.log("WebSocket message received:", event.data);
  displayMessage(event.data);
};

socket.onclose = function (event) {
  console.log("WebSocket is closed now.");
};

socket.onerror = function (error) {
  console.log("WebSocket error:", error);
};

function sendMessage(message) {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify(message));
  } else {
    console.log("WebSocket is not open.");
  }
}

function displayMessage(message) {
  const messageContainer = document.getElementById("messages");
  const messageElement = document.createElement("div");
  messageElement.textContent = message;
  messageContainer.appendChild(messageElement);
}