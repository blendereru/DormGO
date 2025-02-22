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
    let creator: ProtectedResponse
    let members: [ProtectedResponse]
}

struct PostResponse_Update: Codable {
    let message: String
    let post: PostDetails
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
                    self.signalRManager.startConnection()
                    // Restart the connection using the passed instance
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
//    @Published var posts: [PostDetails] = [] // State to hold the posts
//    @Published var posts_update: [PostDetails] = []
    private var customLogger: CustomLogger?
    
    var onPostCreated: ((Post) -> Void)?
      var onPostUpdated: ((Post) -> Void)?
      var onPostDeleted: ((String) -> Void)?
        init() {
            self.customLogger = CustomLogger(signalRManager: self)
            
            guard let logger = customLogger else {
                        fatalError("CustomLogger could not be initialized.")
                    }
        let hubUrl = endpoint("api/posthub")
        hubConnection = HubConnectionBuilder(url: hubUrl)
            .withHttpConnectionOptions { options in
                if let token = getJWTFromKeychain(tokenType: "access_token") {
                    options.accessTokenProvider = { token }
                }
            }
            .withLogging(minLogLevel: .error,logger:  logger)
            .build()
        
                  // Once listeners are set up, mark as ready
              
         // Register listeners for SignalR events
        
    }

    func startConnection() {
        guard let logger = customLogger else {
                   fatalError("CustomLogger could not be initialized.")
               }
//
//          //Rebuild connection with the provided token and custom logger
         self.hubConnection = HubConnectionBuilder(url: endpoint("api/posthub"))
             .withHttpConnectionOptions { options in
                 if let token = getJWTFromKeychain(tokenType: "access_token") {
                     options.accessTokenProvider = { token }
                 }
             }
             .withLogging(minLogLevel: .error, logger:  logger)
             .build()
         setupListeners()
//         // Start the connection
//         self.hubConnection?.start()
        guard hubConnection != nil else {
            print("HubConnection is not initialized")
            return
        }
        hubConnection?.start()
     }
    

    func stopConnection() {
        hubConnection?.stop()
    }

    private func handlemessage_pc(type:Bool,postDto:Post){
        let timestamp = Date()
        print("Received post at \(timestamp) with type: \(type)")  // Log the time and type

        if !type {
            print("Post appended: \(postDto)")
            DispatchQueue.main.async {
                [weak self] in
                               self?.onPostCreated?(postDto)
            }
        } else {
            print("Post ignored due to type being true")
        }
    }
    
   
    private func setupListeners() {
       // let hubUrl = endpoint("api/posthub")
        
    
        hubConnection?.on(method: "PostCreated", callback: { [weak self] (type: Bool, postDto: Post) in
            guard let self = self else {
                   print("Self is nil, cannot handle the post")
                   return
               }

            
                self.handlemessage_pc(type: type, postDto: postDto)
        
          
        })
        
        hubConnection?.on(method: "PostUpdated", callback: { [weak self] ( postDto: Post) in
         
         //   print("Received post update at \(timestamp) with message: \(postDto.message)") // Log the time and type

      
                            DispatchQueue.main.async { [weak self] in
                                self?.onPostUpdated?(postDto)
                            }
            
            
               
            
        })
        
        hubConnection?.on(method: "PostDeleted", callback: { [weak self] (postId: String) in
        
         

            DispatchQueue.main.async { [weak self] in
                    self?.onPostDeleted?(postId)
                }
        })
  
    }

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

             //   self.setupListeners()
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
