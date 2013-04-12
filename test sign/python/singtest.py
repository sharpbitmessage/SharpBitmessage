    #!/usr/bin/python
    import pyelliptic
    from pyelliptic import arithmetic as a

    #privkey  = a.changebase('02ca0020215c5516f277ac6246cbbaad81cd848328bf9bf11e98959e2b991191a71ad81a',16,256)
    pubkey    = a.changebase('02ca0020012e0e59b564c025b15a587da5d33d3599df5e04deca47c783eaed25ebe5af46002032e00af993efc71a2c033a45187918f5b3c03e0e7bb539cecdc0aaa237717db1',16,256)
    signature = a.changebase('30450221008538ac52dbe2b67148e99f23ad78b4c6c4939a26d789ece590c6f1e44a271454022027d4a09e5e74bb3445019a557bd2202154d2510a4df939b9f4645b311255ee37',16,256)
    
    ecc = pyelliptic.ECC(curve='secp256k1',pubkey=pubkey)
    print ecc.verify(signature,'hello')
