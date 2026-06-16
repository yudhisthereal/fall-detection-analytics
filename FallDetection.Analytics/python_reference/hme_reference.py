"""
ANALYTICS SERVER - Homomorphic Computation Party
Responsibilities:
1. Perform homomorphic comparisons on encrypted features
2. Evaluate polynomial on encrypted comparison results

This script acts as a server that:
- Receives encrypted features from Caregiver
- Performs _priv_comp_an (threshold comparisons)
- Performs _priv_comp1_an (encrypted vs encrypted comparisons)
- Sends encrypted comparison results to Caregiver
- Receives re-encrypted comparison results from Caregiver
- Evaluates polynomial on encrypted values
- Sends encrypted polynomial result to Caregiver
"""

import math
import random
import json
import socket
import threading
from datetime import datetime


class AnalyticsServer:
    """
    ANALYTICS Server - Handles all homomorphic computations
    """
    
    def __init__(self, host='localhost', port=5001):
        self.host = host
        self.port = port
        self.server_socket = None
        self.is_running = False
        
        print("[ANALYTICS] ✅ Server initialized")
        print("[ANALYTICS] 🔐 Ready for homomorphic computations")
    
    # ========================================================================
    # HOMOMORPHIC COMPARISON METHODS
    # ========================================================================
    
    def _priv_comp_an(self, cth1, cth2, cs):
        """
        Server-side comparison with plaintext threshold
        Used for: Comparing encrypted value with threshold
        Input: Encrypted value (c1, c2) and threshold cs
        Output: Encrypted comparison result (c111, c121)
        """
        r1 = random.randint(1, 2**22 - 1)
        r2 = random.randint(1, 2**10 - 1)
        c111 = r2 + (r1 * 2 * (cth1 - cs))
        c121 = r2 + (r1 * 2 * (cth2 - cs))
        return c111, c121
    
    def _priv_comp1_an(self, cth11, cth21, cth3, cth4):
        """
        Server-side encrypted vs encrypted comparison
        Used for: Comparing two encrypted values
        Input: Two encrypted values (cth11, cth21) and (cth3, cth4)
        Output: Encrypted comparison result (c11, c12)
        """
        r1 = random.randint(1, 2**22 - 1)
        r2 = random.randint(1, 2**10 - 1)
        c11 = r2 + (r1 * 2 * (cth11 - cth3))
        c12 = r2 + (r1 * 2 * (cth21 - cth4))
        return c11, c12
    
    # ========================================================================
    # POLYNOMIAL EVALUATION METHOD
    # ========================================================================
    
    def evaluate_polynomial(self, encrypted_comparisons):
        """
        Evaluate the polynomial on encrypted comparison results
        Input: Encrypted comparison results with 6 moduli each
        Output: Encrypted polynomial components (pr1-pr6)
        """
        print("\n[ANALYTICS] 🧮 Evaluating polynomial on encrypted comparisons...")
        
        # Extract encrypted comparison components (6 moduli each)
        a = encrypted_comparisons['a']
        b = encrypted_comparisons['b']
        c = encrypted_comparisons['c']
        d = encrypted_comparisons['d']
        e = encrypted_comparisons['e']
        f = encrypted_comparisons['f']
        
        # Extract individual components
        # a = T30 (torso_angle > 30)
        c11 = a['c1']; c21 = a['c2']; c31 = a['c3']
        c41 = a['c4']; c51 = a['c5']; c61 = a['c6']
        
        # b = T40 (thigh_uprightness > 40)
        c12 = b['c1']; c22 = b['c2']; c32 = b['c3']
        c42 = b['c4']; c52 = b['c5']; c62 = b['c6']
        
        # c = T80 (torso_angle > 80)
        c13 = c['c1']; c23 = c['c2']; c33 = c['c3']
        c43 = c['c4']; c53 = c['c5']; c63 = c['c6']
        
        # d = TC (thigh < calf)
        c14 = d['c1']; c24 = d['c2']; c34 = d['c3']
        c44 = d['c4']; c54 = d['c5']; c64 = d['c6']
        
        # e = TL (torso < leg)
        c15 = e['c1']; c25 = e['c2']; c35 = e['c3']
        c45 = e['c4']; c55 = e['c5']; c65 = e['c6']
        
        # f = T60 (thigh_uprightness > 60)
        c16 = f['c1']; c26 = f['c2']; c36 = f['c3']
        c46 = f['c4']; c56 = f['c5']; c66 = f['c6']
        
        # Polynomial evaluation for LSB
        # LSB = (a*b*d) + (a*(1-b)) + (1-c) + ((1-a)*c*(1-f))
        print("  [LSB Polynomial] Computing LSB...")
        pr1l = (c11*c12*c14) + (c11*(1-c12)) + (1-c13) + ((1-c11)*c13*(1-c16))
        pr2l = (c21*c22*c24) + (c21*(1-c22)) + (1-c23) + ((1-c21)*c23*(1-c26))
        pr3l = (c31*c32*c34) + (c31*(1-c32)) + (1-c33) + ((1-c31)*c33*(1-c36))
        pr4l = (c41*c42*c44) + (c41*(1-c42)) + (1-c43) + ((1-c41)*c43*(1-c46))
        pr5l = (c51*c52*c54) + (c51*(1-c52)) + (1-c53) + ((1-c51)*c53*(1-c56))
        pr6l = (c61*c62*c64) + (c61*(1-c62)) + (1-c63) + ((1-c61)*c63*(1-c66))
        
        # Polynomial evaluation for MSB
        # MSB = (a*b*(1-d)*e) + ((1-a)*c*f) + (1-c) + ((1-a)*c*(1-f))
        print("  [MSB Polynomial] Computing MSB...")
        pr1m = (c11*c12*(1-c14)*c15) + ((1-c11)*c13*c16) + (1-c13) + ((1-c11)*c13*(1-c16))
        pr2m = (c21*c22*(1-c24)*c25) + ((1-c21)*c23*c26) + (1-c23) + ((1-c21)*c23*(1-c26))
        pr3m = (c31*c32*(1-c34)*c35) + ((1-c31)*c33*c36) + (1-c33) + ((1-c31)*c33*(1-c36))
        pr4m = (c41*c42*(1-c44)*c45) + ((1-c41)*c43*c46) + (1-c43) + ((1-c41)*c43*(1-c46))
        pr5m = (c51*c52*(1-c54)*c55) + ((1-c51)*c53*c56) + (1-c53) + ((1-c51)*c53*(1-c56))
        pr6m = (c61*c62*(1-c64)*c65) + ((1-c61)*c63*c66) + (1-c63) + ((1-c61)*c63*(1-c66))
        
        # Combine MSB and LSB: result = MSB*2 + LSB
        pr1 = pr1m*2 + pr1l
        pr2 = pr2m*2 + pr2l
        pr3 = pr3m*2 + pr3l
        pr4 = pr4m*2 + pr4l
        pr5 = pr5m*2 + pr5l
        pr6 = pr6m*2 + pr6l
        
        print("  ✅ Polynomial evaluation completed")
        
        return {
            'pr1': pr1, 'pr2': pr2, 'pr3': pr3,
            'pr4': pr4, 'pr5': pr5, 'pr6': pr6
        }
    
    # ========================================================================
    # BUSINESS LOGIC
    # ========================================================================
    
    def perform_comparisons(self, encrypted_features):
        """
        Step 1: Perform all homomorphic comparisons
        Input: Encrypted features from Caregiver
        Output: Encrypted comparison results (to be sent to Caregiver)
        """
        print("\n[ANALYTICS] 🔍 Performing homomorphic comparisons...")
        
        # Extract encrypted features
        Tra1 = encrypted_features['Tra']['ciphertext_1']
        Tra2 = encrypted_features['Tra']['ciphertext_2']
        Tha1 = encrypted_features['Tha']['ciphertext_1']
        Tha2 = encrypted_features['Tha']['ciphertext_2']
        Thl1 = encrypted_features['Thl']['ciphertext_1']
        Thl2 = encrypted_features['Thl']['ciphertext_2']
        cl1 = encrypted_features['cl']['ciphertext_1']
        cl2 = encrypted_features['cl']['ciphertext_2']
        Trl1 = encrypted_features['Trl']['ciphertext_1']
        Trl2 = encrypted_features['Trl']['ciphertext_2']
        ll1 = encrypted_features['ll']['ciphertext_1']
        ll2 = encrypted_features['ll']['ciphertext_2']
        
        comparisons = {}
        
        # 1. Compare torso_angle with 30 (T30)
        print("  ✓ T30: Tra > 30?")
        T301, T302 = self._priv_comp_an(Tra1, Tra2, 3000)
        comparisons['T30'] = (T301, T302)
        
        # 2. Compare thigh_uprightness with 40 (T40)
        print("  ✓ T40: Tha > 40?")
        T401, T402 = self._priv_comp_an(Tha1, Tha2, 4000)
        comparisons['T40'] = (T401, T402)
        
        # 3. Compare torso_angle with 80 (T80)
        print("  ✓ T80: Tra > 80?")
        T801, T802 = self._priv_comp_an(Tra1, Tra2, 8000)
        comparisons['T80'] = (T801, T802)
        
        # 4. Compare thigh_uprightness with 60 (T60)
        print("  ✓ T60: Tha > 60?")
        T601, T602 = self._priv_comp_an(Tha1, Tha2, 6000)
        comparisons['T60'] = (T601, T602)
        
        # 5. Compare thigh_length with calf_length (TC: thigh < calf?)
        print("  ✓ TC: Thl < cl?")
        TC1, TC2 = self._priv_comp1_an(Thl1*10, Thl2*10, cl1*7, cl2*7)
        comparisons['TC'] = (TC1, TC2)
        
        # 6. Compare torso_height with leg_length (TL: torso < leg?)
        print("  ✓ TL: Trl < ll?")
        TL1, TL2 = self._priv_comp1_an(Trl1*10, Trl2*10, ll1*5, ll2*5)
        comparisons['TL'] = (TL1, TL2)
        
        print("  ✅ All comparisons completed")
        
        return comparisons
    
    # ========================================================================
    # SERVER METHODS
    # ========================================================================
    
    def start_server(self):
        """Start the server to listen for requests"""
        try:
            self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.server_socket.bind((self.host, self.port))
            self.server_socket.listen(5)
            self.is_running = True
            
            print(f"\n[ANALYTICS] 🚀 Server listening on {self.host}:{self.port}")
            print("[ANALYTICS] ⏳ Waiting for connections...")
            
            while self.is_running:
                client_socket, address = self.server_socket.accept()
                print(f"\n[ANALYTICS] 📱 Connected to {address}")
                client_thread = threading.Thread(
                    target=self.handle_client,
                    args=(client_socket, address)
                )
                client_thread.start()
                
        except Exception as e:
            print(f"[ANALYTICS] ❌ Server error: {e}")
        finally:
            if self.server_socket:
                self.server_socket.close()
    
    def handle_client(self, client_socket, address):
        """Handle client requests"""
        try:
            data = client_socket.recv(65536).decode('utf-8')
            if not data:
                return
            
            request = json.loads(data)
            operation = request.get('operation')
            
            print(f"[ANALYTICS] 📨 Received operation: {operation}")
            
            response = {}
            
            if operation == 'perform_comparisons':
                # Step 1: Perform homomorphic comparisons
                encrypted_features = request.get('encrypted_features')
                comparisons = self.perform_comparisons(encrypted_features)
                response = {
                    'status': 'success',
                    'operation': 'perform_comparisons',
                    'comparisons': comparisons
                }
                
            elif operation == 'evaluate_polynomial':
                # Step 2: Evaluate polynomial
                encrypted_comparisons = request.get('encrypted_comparisons')
                polynomial_result = self.evaluate_polynomial(encrypted_comparisons)
                response = {
                    'status': 'success',
                    'operation': 'evaluate_polynomial',
                    'polynomial_result': polynomial_result
                }
                
            else:
                response = {
                    'status': 'error',
                    'message': f'Unknown operation: {operation}'
                }
            
            # Send response
            client_socket.send(json.dumps(response).encode('utf-8'))
            print(f"[ANALYTICS] ✅ Response sent for operation: {operation}")
            
        except Exception as e:
            print(f"[ANALYTICS] ❌ Error handling client: {e}")
            error_response = {
                'status': 'error',
                'message': str(e)
            }
            client_socket.send(json.dumps(error_response).encode('utf-8'))
        finally:
            client_socket.close()
    
    def stop_server(self):
        """Stop the server"""
        self.is_running = False
        if self.server_socket:
            self.server_socket.close()
        print("[ANALYTICS] 🛑 Server stopped")


# ============================================================================
# CLIENT FUNCTIONS (for testing)
# ============================================================================

def test_analytics_server():
    """Test the analytics server with sample data"""
    analytics = AnalyticsServer()
    
    print("\n" + "="*80)
    print("TESTING ANALYTICS OPERATIONS")
    print("="*80)
    
    # Simulate encrypted features from Caregiver
    test_features = {
        'Tra': {'ciphertext_1': 123456789, 'ciphertext_2': 987654321},
        'Tha': {'ciphertext_1': 234567890, 'ciphertext_2': 876543210},
        'Thl': {'ciphertext_1': 345678901, 'ciphertext_2': 765432109},
        'cl': {'ciphertext_1': 456789012, 'ciphertext_2': 654321098},
        'Trl': {'ciphertext_1': 567890123, 'ciphertext_2': 543210987},
        'll': {'ciphertext_1': 678901234, 'ciphertext_2': 432109876}
    }
    
    # Test Step 1: Perform comparisons
    comparisons = analytics.perform_comparisons(test_features)
    print("\n✅ Step 1: Comparisons performed")
    
    # Simulate re-encrypted comparisons from Caregiver
    test_reencrypted = {
        'a': {'c1': 111, 'c2': 222, 'c3': 333, 'c4': 444, 'c5': 555, 'c6': 666},
        'b': {'c1': 777, 'c2': 888, 'c3': 999, 'c4': 1010, 'c5': 1111, 'c6': 1212},
        'c': {'c1': 1313, 'c2': 1414, 'c3': 1515, 'c4': 1616, 'c5': 1717, 'c6': 1818},
        'd': {'c1': 1919, 'c2': 2020, 'c3': 2121, 'c4': 2222, 'c5': 2323, 'c6': 2424},
        'e': {'c1': 2525, 'c2': 2626, 'c3': 2727, 'c4': 2828, 'c5': 2929, 'c6': 3030},
        'f': {'c1': 3131, 'c2': 3232, 'c3': 3333, 'c4': 3434, 'c5': 3535, 'c6': 3636}
    }
    
    # Test Step 2: Evaluate polynomial
    polynomial_result = analytics.evaluate_polynomial(test_reencrypted)
    print("\n✅ Step 2: Polynomial evaluated")
    
    print("\n" + "="*80)
    print("✅ All analytics operations tested successfully")
    print("="*80)


# ============================================================================
# MAIN - Run as Server
# ============================================================================

if __name__ == "__main__":
    print("\n" + "="*80)
    print("📊 ANALYTICS HOMOMORPHIC COMPUTATION SERVER")
    print("="*80)
    
    # For testing without network, run operations directly
    test_analytics_server()
    
    # To run as a server, uncomment the code below:
    """
    analytics = AnalyticsServer(host='localhost', port=5001)
    try:
        analytics.start_server()
    except KeyboardInterrupt:
        analytics.stop_server()
        print("\n[ANALYTICS] Server stopped by user")
    """