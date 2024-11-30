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

//signalRManager.startConnection()
