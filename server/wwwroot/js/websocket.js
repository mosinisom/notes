const socket = new WebSocket("ws://localhost:5088/ws");

socket.onopen = function () {
  console.log("WebSocket открыт.");
  checkAuth();
};

socket.onmessage = function (event) {
  const data = JSON.parse(event.data);
  handleResponse(data);
};

socket.onclose = function () {
  console.log("WebSocket закрыт.");
};

socket.onerror = function (error) {
  console.log("Ошибка WebSocket:", error);
};

function sendMessage(message) {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify(message));
  } else {
    console.log("WebSocket не открыт.");
  }
}

function handleResponse(data) {
  console.log(data);
  switch (data.action) {
    case "login":
    case "register":
      if (data.status === "success") {
        localStorage.setItem("auth_token", data.auth_token);
        showNotesContainer();
        getNotesStructure();
      } else {
        alert("Ошибка: " + data.message);
      }
      break;
    case "get_note_structure":
      if (data.status === "success") {
        renderNotesTree(data.structure);
      } else {
        alert("Ошибка получения заметок: " + data.message);
      }
      break;
  }
}

function checkAuth() {
  const authToken = localStorage.getItem("auth_token");
  if (authToken) {
    showNotesContainer();
    getNotesStructure();
  } else {
    document.getElementById("auth-container").style.display = "block";
  }
}

document.getElementById("login-btn").addEventListener("click", function () {
  const username = document.getElementById("username").value;
  const passwordHash = md5(document.getElementById("password").value);
  sendMessage({ action: "login", username: username, password_hash: passwordHash });

  console.log("login", username, passwordHash);
});

document.getElementById("register-btn").addEventListener("click", function () {
  const username = document.getElementById("username").value;
  const passwordHash = md5(document.getElementById("password").value);
  sendMessage({ action: "register", username: username, password_hash: passwordHash });

  console.log("register", username, passwordHash);
});

function showNotesContainer() {
  document.getElementById("auth-container").style.display = "none";
  document.getElementById("notes-container").style.display = "block";
}

function getNotesStructure() {
  const authToken = localStorage.getItem("auth_token");
  sendMessage({ action: "get_note_structure", auth_token: authToken });
}

function renderNotesTree(structure) {
  const container = document.getElementById("notes-tree");
  container.innerHTML = "";
  structure.forEach(item => {
    if (item.is_folder) {
      const folder = document.createElement("div");
      folder.className = "folder-item";
      folder.textContent = item.title;
      container.appendChild(folder);

      if (item.children) {
        const childContainer = document.createElement("div");
        childContainer.className = "child-container";
        folder.appendChild(childContainer);
        renderNotesTreeRecursive(item.children, childContainer);
      }
    } else {
      const note = document.createElement("div");
      note.className = "note-item";
      note.textContent = item.title;
      container.appendChild(note);
    }
  });
}

function renderNotesTreeRecursive(items, container) {
  items.forEach(item => {
    if (item.is_folder) {
      const folder = document.createElement("div");
      folder.className = "folder-item";
      folder.textContent = item.title;
      container.appendChild(folder);
      if (item.children) {
        const childContainer = document.createElement("div");
        childContainer.className = "child-container";
        folder.appendChild(childContainer);
        renderNotesTreeRecursive(item.children, childContainer);
      }
    } else {
      const note = document.createElement("div");
      note.className = "note-item";
      note.textContent = item.title;
      container.appendChild(note);
    }
  });
}

