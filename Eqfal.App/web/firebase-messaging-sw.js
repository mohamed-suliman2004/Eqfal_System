importScripts("https://www.gstatic.com/firebasejs/10.7.0/firebase-app-compat.js");
importScripts("https://www.gstatic.com/firebasejs/10.7.0/firebase-messaging-compat.js");

const firebaseConfig = {
  apiKey: "AIzaSyA_uBMDaC20p4dOR5S1WYD-CM86wSenLbI",
  authDomain: "eqfal-system-81268.firebaseapp.com",
  projectId: "eqfal-system-81268",
  storageBucket: "eqfal-system-81268.firebasestorage.app",
  messagingSenderId: "100233760473",
  appId: "1:100233760473:web:8cb195206ac0bfb94bb8c8",
  measurementId: "G-2PG5N3TXQP"
};

firebase.initializeApp(firebaseConfig);

const messaging = firebase.messaging();

messaging.onBackgroundMessage((payload) => {
  console.log('[firebase-messaging-sw.js] Received background message ', payload);
  const notificationTitle = payload.notification.title;
  const notificationOptions = {
    body: payload.notification.body,
    icon: '/icons/Icon-192.png'
  };

  self.registration.showNotification(notificationTitle, notificationOptions);
});
