//
//  notifications.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 29.11.2024.
//
import SwiftUI
import SignalRClient
import Foundation


struct PostDetails: Codable {
    let postId: String
    let description: String
    let currentPrice: Double
    let latitude: Double
    let longitude: Double
    let createdAt: String
    let maxPeople: Int
    let members: [ProtectedResponse]
}


class CustomLogger: Logger {
    let dateFormatter: DateFormatter
    var signalRManager: SignalRManager

    init(signalRManager: SignalRManager) {
        self.signalRManager = signalRManager
        dateFormatter = DateFormatter()
        dateFormatter.calendar = Calendar(identifier: .iso8601)
        dateFormatter.locale = Locale(identifier: "en_US_POSIX")
        dateFormatter.timeZone = TimeZone(secondsFromGMT: 0)
        dateFormatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSSXXXXX"
    }

    func log(logLevel: LogLevel, message: @autoclosure () -> String) {
        let logMessage = message()
        let timestamp = dateFormatter.string(from: Date())
        
        if logLevel == .error && logMessage.contains("401") {
            print("\(timestamp) ERROR: Unauthorized access detected (401). Please verify your token or credentials.")
            PostAPIManager().refreshToken2 { success in
                if success {
                    print("Token refreshed in custom logger successfully. Restarting connection.")
                    SignalRManager().startConnection()  // Restart the connection using the passed instance
                } else {
                    print("Failed to refresh token. Cannot restart connection.")
                }
            }
        } else {
            print("\(timestamp) \(logLevel.toString()): \(logMessage)")
        }
    }
}

class SignalRManager: ObservableObject{

    private var hubConnection: HubConnection?
    @Published var posts: [PostDetails] = [] // State to hold the posts
    @Published var posts_update: [PostDetails] = []
    init() {
        let hubUrl = endpoint("api/posthub")
        hubConnection = HubConnectionBuilder(url: hubUrl)
            .withHttpConnectionOptions { options in
                if let token = getJWTFromKeychain(tokenType: "access_token") {
                    options.accessTokenProvider = { token }
                }
            }
            .withLogging(minLogLevel: .error,logger: CustomLogger(signalRManager: self))
            .build()

        setupListeners() // Register listeners for SignalR events
    }

    func startConnection() {
        
//        refreshTokenIfNeeded { [weak self] success in
//            if success {
                self.hubConnection?.start()
//            } else {
//                print("Failed to refresh token. Cannot start SignalR connection.")
//            }
//        }
    }
    
    

    func stopConnection() {
        hubConnection?.stop()
    }

    private func handlemessage_pc(type:Bool,postDto:PostDetails){
        let timestamp = Date()
        print("Received post at \(timestamp) with type: \(type)")  // Log the time and type

        if !type {
            print("Post appended: \(postDto)")
            DispatchQueue.main.async {
                self.posts.append(postDto)
            }
        } else {
            print("Post ignored due to type being true")
        }
    }
    
   
    private func setupListeners() {
       // let hubUrl = endpoint("api/posthub")
        
    
        hubConnection?.on(method: "PostCreated", callback: { [weak self] (type: Bool, postDto: PostDetails) in
            guard let self = self else {
                   print("Self is nil, cannot handle the post")
                   return
               }

            
                self.handlemessage_pc(type: type, postDto: postDto)
        
          
        })
        
        hubConnection?.on(method: "PostUpdated", callback: { [weak self] (type: Bool, postDto: PostDetails) in
            let timestamp = Date()
            print("Received post update at \(timestamp) with type: \(type)") // Log the time and type

            if !type {
                DispatchQueue.main.async {
                    if let index = self?.posts.firstIndex(where: { $0.postId == postDto.postId }) {
                        // Update the existing post
                        self?.posts[index] = postDto
                        print("Post updated: \(postDto)")
                    } else {
                        print("Post not found; skipping update.")
                    }
                }
            } else {
                print("Post ignored due to type being true")
            }
        })
        
        hubConnection?.on(method: "PostDeleted", callback: { [weak self] (postId: String) in
            let timestamp = Date()
            print("Post deleted notification received at \(timestamp) for PostId: \(postId)")

            DispatchQueue.main.async {
                if let index = self?.posts.firstIndex(where: { $0.postId == postId }) {
                    self?.posts.remove(at: index)
                    print("Post deleted: \(postId)")
                } else {
                    print("Post not found; skipping deletion.")
                }
            }
        })    }

    private func refreshTokenIfNeeded(completion: @escaping (Bool) -> Void) {
      

        // Aways attempt to refresh the token
        refreshToken { success in
            completion(success)
        }
    }

    private func refreshToken(completion: @escaping (Bool) -> Void) {
        PostAPIManager().refreshToken2 { [weak self] success in
            if success {
                print("Token refreshed successfully. Restarting connection.")
                self?.restartConnection()
                completion(true)
            } else {
                print("Failed to refresh token. Cannot restart connection.")
               // completion(false)
            }
        }
    }

    private func restartConnection() {
        if let newToken = getJWTFromKeychain(tokenType: "access_token") {
            hubConnection?.stop()

            // Delay the start of the new connection to ensure proper stopping of the old connection
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                self.hubConnection = HubConnectionBuilder(url: endpoint("api/posthub"))
                    .withHttpConnectionOptions { options in
                        options.accessTokenProvider = { newToken }
                    }
                    .withLogging(minLogLevel: .info)
                    .build()

                self.setupListeners()
                self.hubConnection?.start()  // Reconnect after token refresh
            }
        }
    }
    private func isTokenExpired(_ token: String) -> Bool {
        // Token expiration check logic (e.g., decoding token and checking expiry)
        return false  // Replace with actual expiration check
    }
}

let signalRManager = SignalRManager()
struct ConfirmationTokens {
    let userName: String
    let accessToken: String
    let refreshToken: String
}

class ConfirmationManager: ObservableObject {
    private var hubConnection: HubConnection?
    @Published var isAuthenticated: Bool = false
    @Published var errorMessage: String? = nil

    private var email: String?

    init(email: String="") {
        self.email = email
    }
    func setEmail(_ newEmail: String) {
            self.email = newEmail
        }
    func connectToServer() {
        guard let email = email else {
            print("Email is not set.")
            errorMessage = "Email is not set."
            return
        }

        let baseUrlString = endpoint("api/userhub")
        let hubUrlString = "\(baseUrlString)?userName=\(email)"
        
        guard let hubUrl = URL(string: hubUrlString) else {
            print("Invalid URL.")
            errorMessage = "Invalid URL."
            return
        }

        hubConnection = HubConnectionBuilder(url: hubUrl)
            .withLogging(minLogLevel: .debug)
            .build()

        setupListeners()
        hubConnection?.start()
    }

    func stopConnection() {
        hubConnection?.stop()
    }

    private func setupListeners() {
        hubConnection?.on(method: "EmailConfirmed", callback: { [weak self] (userName: String, tokenData: [String: String]) in
            guard let self = self else { return }

            print("Received EmailConfirmed event:")
            print("User Name: \(userName)")

            // Extract tokens from the dictionary
            guard let accessToken = tokenData["accessToken"], let refreshToken = tokenData["refreshToken"] else {
                print("Error: Missing tokens in the payload.")
                self.errorMessage = "Error: Missing tokens in the payload."
                return
            }

            print("Access Token: \(accessToken)")
            print("Refresh Token: \(refreshToken)")

            DispatchQueue.global().async {
                let isAccessTokenSaved = saveJWTToKeychain(token: accessToken, tokenType: "access_token")
                let isRefreshTokenSaved = saveJWTToKeychain(token: refreshToken, tokenType: "refresh_token")

                DispatchQueue.main.async {
                    if isAccessTokenSaved && isRefreshTokenSaved {
                        print("Tokens saved successfully!")
                        UserDefaults.standard.set(true, forKey: "isAuthenticated")
                        self.isAuthenticated = true
                    } else {
                        print("Error saving tokens.")
                        self.errorMessage = "Error saving tokens."
                    }
                }
            }
        })
    }
}
