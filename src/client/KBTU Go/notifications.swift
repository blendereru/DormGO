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

class SignalRManager: ObservableObject {
    private var hubConnection: HubConnection?
    @Published var posts: [PostDetails] = [] // State to hold the posts

    init() {
        let hubUrl = endpoint("api/posthub")
        hubConnection = HubConnectionBuilder(url: hubUrl)
            .withHttpConnectionOptions { options in
                if let token = getJWTFromKeychain(tokenType: "access_token") {
                    options.accessTokenProvider = { token }
                }
            }
            .withLogging(minLogLevel: .info)
            .build()
        
        setupListeners() // Register listeners for SignalR events
    }

    func startConnection() {
        hubConnection?.start()
    }

    func stopConnection() {
        hubConnection?.stop()
    }

    private func setupListeners() {
        hubConnection?.on(method: "PostCreated", callback: { [weak self] (userName: String, postDto: PostDetails) in
            print("Received PostCreated event from user: \(userName)")

            DispatchQueue.main.async {
                self?.posts.append(postDto)
            }
        })
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
