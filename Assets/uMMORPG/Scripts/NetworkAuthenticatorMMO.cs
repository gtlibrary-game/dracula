using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Mirror;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.ServerModels;
using System.Collections.Generic;
using TMPro;

public class NetworkAuthenticatorMMO : NetworkAuthenticator
{
    [Header("Components")]
    public NetworkManagerMMO manager;

    // login info for the local player
    // we don't just name it 'account' to avoid collisions in handshake
    [Header("Login")]
    public string loginAccount = "";
    public string loginPassword = "";

    [Header("Security")]
    public string passwordSalt = "at_least_16_byte";
    public int accountMaxLength = 256;
    public string playFabId;
    public string sessionTicket;


    public void SendSignedTicket(string signature) {

        //ThirdwebManager.IsConnectedWallet();

        var message = new SignTicketMsg {
            signature = signature,
            playFabId = playFabId,
        };
        NetworkClient.connection.Send(message);
    }
    
    Dictionary<string, string> playFabToAccount = new Dictionary<string, string>();
    void OnSignSessionTicket(AuthenticateSessionTicketResult result) {
        Debug.Log("Playfab authenticated...");

        NetworkConnectionToClient conn = ticketToConn[result.UserInfo.PlayFabId];

        // login successful
        Debug.Log("Signing ticket was successful so far...: " + result.UserInfo.PlayFabId);

        // notify client about successful login. otherwise it
        // won't accept any further messages.
        conn.Send(new SignTicketSuccessMsg());

        // Need to save the acccount address in the connection
        string account = playFabToAccount[result.UserInfo.PlayFabId];
        //conn.account = account; // FIXME: need to turn this part on

        // authenticate on server
        //OnServerAuthenticated.Invoke(conn);
    }

    public void OnSignedTicket(NetworkConnectionToClient conn, SignTicketMsg message) {
        Debug.LogWarning("Ticket was signed:" + message);

        // Need to check if the playFab sessionTicket matches the account
        var request = new AuthenticateSessionTicketRequest {
            SessionTicket = message.sessionTicket
        };
                        
        ticketToConn[message.playFabId] = conn;         //FIXME
        playFabToAccount[message.playFabId] = "0xAcc"; // ThirdWeb... await sdk.wallet.RecoverAddress(message.sessionTicket, message.signature);

        PlayFabServerAPI.AuthenticateSessionTicket(request, OnSignSessionTicket, OnError , conn);
    }

    public void RegisterEmail() {
        var request = new RegisterPlayFabUserRequest {
            Email = loginAccount,
            Password = loginPassword,
            RequireBothUsernameAndEmail = false
        }; 
        
        PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnError);
        Debug.Log("In register");
    }
    //void OnRegisterSuccess(RegisterPlayFabUserResult result) {
    void OnRegisterSuccess(RegisterPlayFabUserResult result) {
        Debug.LogWarning("You are now registred.");
        // PlayFabClientAPI.Logout();
    }
    void OnError(PlayFabError error) {
        Debug.LogWarning(error);
        manager.uiPopup.Show("" + error);
    }

    public void LoginUser() {
        Debug.Log("In LoginUser");
        var request = new LoginWithEmailAddressRequest {
            Email = loginAccount,
            Password = loginPassword
        };
        PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnError);
    }
    public void OnLoginSuccess(LoginResult result) {
        playFabId = result.PlayFabId;
        sessionTicket = result.SessionTicket;

        Debug.Log("Logged in with PlayFab ID: " + playFabId);
        Debug.Log("Session ticket: " + sessionTicket);
        manager.StartClient();
        OnClientAuthenticate();
    }
    public void ResetPassword() {
        var request = new SendAccountRecoveryEmailRequest {
            Email = loginAccount,
            TitleId = "C0C36"
        };
        PlayFabClientAPI.SendAccountRecoveryEmail(request, OnPasswordReset, OnError);
    }
    public void OnPasswordReset(SendAccountRecoveryEmailResult result) {
        Debug.Log("Password recovery email sent.");
    }


    // client //////////////////////////////////////////////////////////////////
    public override void OnStartClient()
    {
        // register login success message, allowed before authenticated
        NetworkClient.RegisterHandler<LoginSuccessMsg>(OnClientLoginSuccess, false);
        //NetworkClient.RegisterHandler<LoginWrongUser>(OnLoginWrongUserResult, false);
        //NetworkClient.RegisterHandler<RegisterSuccessMsg>(OnRegisterResult, false);
    }

    public override void OnClientAuthenticate()
    {
        print("=================OnClientAuthenticate================");
        // send login packet with hashed password, so that the original one
        // never leaves the player's computer.
        //
        // it's recommended to use a different salt for each hash. ideally we
        // would store each user's salt in the database. to not overcomplicate
        // things, we will use the account name as salt (at least 16 bytes)
        //
        // Application.version can be modified under:
        // Edit -> Project Settings -> Player -> Bundle Version
        string hash = Utils.PBKDF2Hash(loginPassword, passwordSalt + loginAccount);
        LoginMsg message = new LoginMsg{account=loginAccount, password=hash, version=Application.version, playFabId=playFabId, sessionTicket=sessionTicket};
        NetworkClient.connection.Send(message);
        Debug.Log("login message was sent");

        // set state
        manager.state = NetworkState.Handshake;
    }

    void OnClientLoginSuccess(LoginSuccessMsg msg)
    {
        // authenticated successfully. OnClientConnected will be called.
        OnClientAuthenticated.Invoke();
    }
    void OnRegisterResult(RegisterSuccessMsg msg)
    {
        print(msg.msg);
    }
    void OnLoginWrongUserResult(LoginWrongUser msg)
    {
        print(msg.msg);
    }
    public void OnClientRegister()
    {
        /*
        string hash = Utils.PBKDF2Hash(loginPassword, passwordSalt + loginAccount);
        RegisterMsg message = new RegisterMsg{account=loginAccount, password=hash, version=Application.version, playFabId=playFabId, sessionTicket=sessionTicket};
        NetworkClient.connection.Send(message);
        Debug.Log("Register message was sent");
        manager.state = NetworkState.Handshake;
        */
    }
    
    // server //////////////////////////////////////////////////////////////////
    public override void OnStartServer()
    {
        // register login message, allowed before authenticated
        NetworkServer.RegisterHandler<LoginMsg>(OnServerLogin, false);
        NetworkServer.RegisterHandler<SignTicketMsg>(OnSignedTicket, false);
        //NetworkServer.RegisterHandler<RegisterMsg>(OnServerRegister, false);
        // NetworkServer.RegisterHandler<ResetPasswordMsg>(OnServerResetPassword, false);
    }


    public override void OnServerAuthenticate(NetworkConnectionToClient conn)
    {
        // wait for LoginMsg from client
    }

    // virtual in case someone wants to modify
    public virtual bool IsAllowedAccountName(string account)
    {
        return true;
        // not too long?
        // only contains letters, number and underscore and not empty (+)?
        // (important for database safety etc.)
        //return account.Length <= accountMaxLength &&
               //Regex.IsMatch(account, @"^[a-zA-Z0-9_\.]+$");
    }

    bool AccountLoggedIn(string account)
    {
        // in lobby or in world?
        return manager.lobby.ContainsValue(account) ||
               Player.onlinePlayers.Values.Any(p => p.account == account);
    }

    void OnServerRegister(NetworkConnectionToClient conn, RegisterMsg message)
    {
        /*
        if (message.version == Application.version)
        {
            // allowed account name?
            if (IsAllowedAccountName(message.account))
            {
               
                if (Database.singleton.TryRegister(message.account, message.password))
                {
                   conn.Send(new RegisterSuccessMsg{ msg = "Register has been successful!" });
                }else{
                    conn.Send(new RegisterSuccessMsg{ msg = "You already has been registered" });
                }
            }
        }
        */
    }
    // void OnServerResetPassword(NetworkConnectionToClient conn, ResetPasswordMsg message)
    // {

    // }

    Dictionary<string, NetworkConnectionToClient> ticketToConn = new Dictionary<string, NetworkConnectionToClient>();

    void OnAuthenticateSessionTicket(AuthenticateSessionTicketResult result) {
        Debug.Log("Playfab authenticated...");

        NetworkConnectionToClient conn = ticketToConn[result.UserInfo.PlayFabId];

        // login successful
        Debug.Log("login successful: " + result.UserInfo.PlayFabId);

        // notify client about successful login. otherwise it
        // won't accept any further messages.
        conn.Send(new LoginSuccessMsg());

        // authenticate on server
        OnServerAuthenticated.Invoke(conn);
    }
    
    void OnServerLogin(NetworkConnectionToClient conn, LoginMsg message)
    {
        print("=======================NetworkConnectionToClient==========================");
        // correct version?
        if (message.version == Application.version)
        {
            // allowed account name?
            if (IsAllowedAccountName(message.account))
            {
               
                if (Database.singleton.TryLogin(message.account, "message.password"))
                {
                    // not in lobby and not in world yet?
                    if (!AccountLoggedIn(message.account))
                    {
                        // add to logged in accounts
                        manager.lobby[conn] = message.account;

                        // Need to check if the playFab sessionTicket matches the account
                        var request = new AuthenticateSessionTicketRequest {
            	            SessionTicket = message.sessionTicket
        	            };
                        
                        ticketToConn[message.playFabId] = conn;

                        PlayFabServerAPI.AuthenticateSessionTicket(request, OnAuthenticateSessionTicket, OnError , conn);

                        // login successful
                        //Debug.Log("login successful: " + message.account);

                        // notify client about successful login. otherwise it
                        // won't accept any further messages.
                        //conn.Send(new LoginSuccessMsg());

                        // authenticate on server
                        //OnServerAuthenticated.Invoke(conn);
                    }
                    else
                    {
                        Debug.Log("account already logged in: " + message.account);
                        manager.ServerSendError(conn, "already logged in", true);

                        // note: we should disconnect the client here, but we can't as
                        // long as unity has no "SendAllAndThenDisconnect" function,
                        // because then the error message would never be sent.
                        //conn.Disconnect();
                    }
                }
            }
            else
            {
                //Debug.Log("account name not allowed: " + message.account); <- don't show on live server
                manager.ServerSendError(conn, "account name not allowed", true);
            }
        }
        else
        {
            //Debug.Log("version mismatch: " + message.account + " expected:" + Application.version + " received: " + message.version); <- don't show on live server
            manager.ServerSendError(conn, "outdated version", true);
        }
    }
}
